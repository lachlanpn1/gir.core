using System;

namespace GObject
{
    public partial class Object
    {
        private class ToggleRef<T> where T : Object
        {
            private object reference;

            public T? Object
            {
                get
                {
                    if (reference is T obj)
                        return obj;

                    if (reference is WeakReference<T> weakRef && weakRef.TryGetTarget(out T? refObj))
                        return refObj;

                    return null;
                }
            }
            
            public ToggleRef(T obj)
            {
                reference = obj;
                Native.add_toggle_ref(obj.Handle, ToggleReference, IntPtr.Zero);
            }

            private void ToggleReference(IntPtr data, IntPtr @object, bool is_last_ref)
            {
                if (is_last_ref && reference is T obj)
                {
                    reference = new WeakReference<T>(obj);
                }
                else if (!is_last_ref && reference is WeakReference<T> weakRef)
                {
                    if (weakRef.TryGetTarget(out T? weakObj))
                        reference = weakObj;
                    else
                        throw new Exception("Could not toggle reference to strong. It is garbage collected.");

                }
            }
        }   
    }
}
