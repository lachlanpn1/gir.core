using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using GLib;

namespace GObject
{
    public partial class Object : IObject, INotifyPropertyChanged, IDisposable, IHandle
    {
        #region Fields

        private static readonly Dictionary<IntPtr, ToggleRef<Object>> SubclassObjects = new ();
        private static readonly Dictionary<IntPtr, WeakReference<Object>> WrapperObjects = new ();
        private static readonly Dictionary<ClosureHelper, ulong> Closures = new ();

        #endregion

        #region Events

        /// <summary>
        /// Event triggered when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        #endregion

        #region Properties
        
        public IntPtr Handle { get; private set; }
        
        // We need to store a reference to WeakNotify to
        // prevent the delegate from being collected by the GC
        private WeakNotify? _onFinalized;

        private bool Disposed { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Constructs a new object
        /// </summary>
        /// <param name="properties"></param>
        public Object(params ConstructParameter[] properties)
        {
            // This will automatically register our
            // type in the type dictionary. If the type is
            // a user-subclass, it will register it with
            // the GType type system automatically.
            System.Type? t = GetType();
            Type typeId = TypeDictionary.Get(t);
            Console.WriteLine($"Instantiating {TypeDictionary.Get(typeId)}");

            // Pointer to GObject
            IntPtr handle;

            // Handle Properties
            var nProps = properties.Length;

            // TODO: Remove dual branches
            if (nProps > 0)
            {
                // We have properties
                // Prepare Construct Properties
                var names = new IntPtr[nProps];
                var values = new Value[nProps];

                // Populate arrays
                for (var i = 0; i < properties.Length; i++)
                {
                    ConstructParameter? prop = properties[i];
                    // TODO: Marshal in a block, rather than one at a time
                    // for performance reasons.
                    names[i] = Marshal.StringToHGlobalAnsi(prop.Name);
                    values[i] = prop.Value;
                }

                // Create with properties
                handle = Native.new_with_properties(
                    typeId.Value,
                    (uint) names.Length,
                    ref names[0],
                    values
                );

                // Free strings
                foreach (IntPtr ptr in names)
                    Marshal.FreeHGlobal(ptr);
            }
            else
            {
                // Construct with no properties
                IntPtr zero = IntPtr.Zero;
                handle = Native.new_with_properties(
                    typeId.Value,
                    0,
                    ref zero,
                    System.Array.Empty<Value>()
                );
            }

            Initialize(handle);
        }

        /// <summary>
        /// Initializes a wrapper for an existing object
        /// </summary>
        /// <param name="handle"></param>
        protected Object(IntPtr handle)
        {
            // TODO: Check to make sure the handle matches our
            // wrapper type.
            Initialize(handle);
        }

        ~Object() => Dispose(false);

        #endregion

        #region Methods

        private void Initialize(IntPtr ptr)
        {
            Handle = ptr;

            RegisterObject();
            RegisterProperties();
            RegisterOnFinalized();

            Initialize();
        }

        /// <summary>
        /// Wrapper and subclasses can override here to perform immediate initialization
        /// </summary>
        protected virtual void Initialize() { }

        // Modify this in the future to play nicely with virtual function support?
        private void OnFinalized(IntPtr data, IntPtr where_the_object_was)
        {
            DisposeManagedState();
            SetDisposed();
        }

        private void RegisterOnFinalized()
        {
            _onFinalized = OnFinalized;
            Native.weak_ref(Handle, _onFinalized, IntPtr.Zero);
        }

        private void RegisterObject()
        {
            if(IsSubclass(GetType()))
                SubclassObjects.Add(Handle, this);
            else
                WrapperObjects.Add(Handle, new WeakReference<Object>(this));
        }
        
        private void RegisterProperties()
        {
            const System.Reflection.BindingFlags PropertyDescriptorFieldFlags = System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.FlattenHierarchy;

            const System.Reflection.BindingFlags MethodFlags = System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic;

            foreach (System.Reflection.FieldInfo? field in GetType().GetFields(PropertyDescriptorFieldFlags))
            {
                if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(Property<>))
                {
                    System.Reflection.MethodInfo? method =
                        field.FieldType.GetMethod(nameof(Property<Object>.RegisterNotifyEvent), MethodFlags);
                    method?.Invoke(field.GetValue(this), new object[] {this});
                }
            }
        }

        // Property Notify Events
        protected internal void RegisterNotifyPropertyChangedEvent(string propertyName, Action callback)
            => RegisterEvent($"notify::{propertyName}", callback);

        protected internal void RegisterEvent(string eventName, ActionRefValues callback, bool after = false)
        {
            ThrowIfDisposed();
            RegisterEvent(eventName, new ClosureHelper(this, callback), after);
        }

        protected internal void RegisterEvent(string eventName, Action callback, bool after = false)
        {
            ThrowIfDisposed();
            RegisterEvent(eventName, new ClosureHelper(this, callback), after);
        }

        private void RegisterEvent(string eventName, ClosureHelper closure, bool after)
        {
            if (Closures.TryGetValue(closure, out var id) && Global.Native.signal_handler_is_connected(Handle, id))
                return; // Skip if the handler is already registered

            var ret = Global.Native.signal_connect_closure(Handle, eventName, closure.Handle, after);

            if (ret == 0)
                throw new Exception($"Could not connect to event {eventName}");

            // Add to our closures list so the callback doesn't get garbage collected.
            Closures[closure] = ret;
        }

        protected internal void UnregisterEvent(ActionRefValues callback)
        {
            ThrowIfDisposed();

            if (ClosureHelper.TryGetByDelegate(callback, out ClosureHelper? closure))
                UnregisterEvent(closure);
        }

        protected internal void UnregisterEvent(Action callback)
        {
            ThrowIfDisposed();

            if (ClosureHelper.TryGetByDelegate(callback, out ClosureHelper? closure))
                UnregisterEvent(closure);
        }

        private void UnregisterEvent(ClosureHelper closure)
        {
            if (!Closures.TryGetValue(closure, out var id))
                return;

            Global.Native.signal_handler_disconnect(Handle, id);
            Closures.Remove(closure);
        }

        protected void ThrowIfDisposed()
        {
            if (Disposed)
                throw new Exception("Object is disposed");
        }
        
        /// <summary>
        /// Notify this object that a property has just changed.
        /// </summary>
        /// <param name="propertyName">The name of the property who changed.</param>
        protected internal void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            if (propertyName == null)
                return;

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// This function returns the proxy object to the provided handle
        /// if it already exists, otherwise creates a new wrapper object
        /// and returns it. Note that <typeparamref name="T"/> is the type
        /// the object should be returned. It is independent of the object's
        /// actual type and is provided purely for convenience.
        /// </summary>
        /// <param name="handle">A pointer to the native GObject that should be wrapped.</param>
        /// <typeparam name="T"></typeparam>
        /// <returns>A C# proxy object which wraps the native GObject.</returns>
        /// <exception cref="NullReferenceException"></exception>
        /// <exception cref="InvalidCastException"></exception>
        /// <exception cref="Exception"></exception>
        public static T WrapHandle<T>(IntPtr handle)
            where T : GObject.Object
        {
            if (handle == IntPtr.Zero)
                throw new NullReferenceException(
                    $"Failed to wrap handle as type <{typeof(T).FullName}>. Null handle passed to WrapHandle.");

            if (WrapperObjects.TryGetValue(handle, out WeakReference<Object>? weakReference))
            {
                if (weakReference.TryGetTarget(out Object? obj))
                    return (T) obj;

                WrapperObjects.Remove(handle);
            }

            // Resolve GType of object
            Type trueGType = TypeFromHandle(handle);
            System.Type? trueType = null;

            // Ensure 'T' is registered in type dictionary for future use. It is an error for a
            // wrapper type to not define a TypeDescriptor. 
            TypeDescriptor desc = TypeDescriptorRegistry.ResolveTypeDescriptorForType(typeof(T));
            
            TypeDictionary.AddRecursive(typeof(T), desc.GType);
            
            // Optimisation: Compare the gtype of 'T' to the GType of the pointer. If they are
            // equal, we can skip the type dictionary's (possible) recursive lookup and return
            // immediately.
            if (desc.GType.Equals(trueGType))
            {
                // We are actually a type 'T'.
                // The conversion will always be valid
                trueType = typeof(T);
            }
            else
            {
                // We are some other representation of 'T' (e.g. a more derived type)
                // Retrieve the normal way
                trueType = TypeDictionary.Get(trueGType);
                
                // Ensure the conversion is valid
                Type castGType = TypeDictionary.Get(typeof(T));
                if (!Global.Native.type_is_a(trueGType.Value, castGType.Value))
                    throw new InvalidCastException();
            }

            // Create using 'IntPtr' constructor
            System.Reflection.ConstructorInfo? ctor = trueType.GetConstructor(
                System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Instance,
                null, new[] { typeof(IntPtr) }, null
            );
            
            if (ctor == null)
                throw new Exception($"Type {trueType.FullName} does not define an IntPtr constructor. This could mean improperly defined bindings");

            return (T) ctor.Invoke(new object[] { handle });
        }

        /// <summary>
        /// A variant of <see cref="WrapHandle{T}"/> which fails gracefully if the pointer cannot be wrapped.
        /// </summary>
        /// <param name="handle">A pointer to the native GObject that should be wrapped.</param>
        /// <param name="o">A C# proxy object which wraps the native GObject.</param>
        /// <typeparam name="T"></typeparam>
        /// <returns><c>true</c> if the handle was wrapped, or <c>false</c> if something went wrong.</returns>
        public static bool TryWrapHandle<T>(IntPtr handle, [NotNullWhen(true)] out T? o)
            where T : Object
        {
            o = null;
            try
            {
                o = WrapHandle<T>(handle);
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Could not wrap handle as type {typeof(T).FullName}: {e.Message}");
                return false;
            }
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing)
        {
            if (Disposed)
                return;

            if(disposing)
                DisposeManagedState();

            DisposeUnmanagedState();
            SetDisposed();
        }

        protected void SetDisposed()
        {
            Disposed = true;
        }

        protected virtual void DisposeManagedState()
        {
            Handle = IntPtr.Zero;
            WrapperObjects.Remove(Handle);
            
            // TODO: Find out about closure release
            /*foreach(var closure in closures)
                closure.Dispose();*/

            // TODO activate: closures.Clear();
        }
        
        protected virtual void DisposeUnmanagedState()
        {
            Native.unref(Handle);
        }

        #endregion
    }
}
