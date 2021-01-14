﻿using System;
using GLib;
using GObject;

namespace GdkPixbuf
{
    public partial class Pixbuf
    {
        #region Properties
        
        #region WidthProperty

        public static readonly Property<int> WidthProperty = Property<int>.Wrap<Pixbuf>(
            Native.WidthProperty,
            nameof(Width),
            get: (o) => o.Width,
            set: (o, v) => o.Width = v
        );

        public int Width
        {
            get => GetProperty(WidthProperty);
            set => SetProperty(WidthProperty, value);
        }
        
        #endregion
        
        #region HeightProperty

        public static readonly Property<int> HeightProperty = Property<int>.Wrap<Pixbuf>(
            Native.HeightProperty,
            nameof(Height),
            get: (o) => o.Height,
            set: (o, v) => o.Height = v
        );

        public int Height
        {
            get => GetProperty(HeightProperty);
            set => SetProperty(HeightProperty, value);
        }
        
        #endregion
        
        
        #endregion
        public static Pixbuf NewFromFile(string fileName)
        {
            IntPtr handle = Native.new_from_file(fileName, out IntPtr error);
            Error.ThrowOnError(error);

            return WrapHandle<Pixbuf>(handle, true);
        }

    }
}