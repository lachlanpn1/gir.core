﻿using Gdk;

namespace Gtk
{
    public partial class ColorChooserWidget
    {
        #region IColorChooser Implementation

        public RGBA Rgba
        {
            get => GetProperty(ColorChooser.RgbaProperty);
            set => SetProperty(ColorChooser.RgbaProperty, value);
        }

        public bool UseAlpha
        {
            get => GetProperty(ColorChooser.UseAlphaProperty);
            set => SetProperty(ColorChooser.UseAlphaProperty, value);
        }

        #endregion
    }
}
