﻿using System;
using System.Reflection;
using GObject;
using Gtk;
using Type = GObject.Type;

namespace GtkDemo
{
    public class CompositeWidget : Bin
    {
        private static void ClassInit(Type gClass, System.Type type, IntPtr classData)
        {
            SetTemplate2(
                gtype: gClass, 
                template: Assembly.GetExecutingAssembly().ReadResource("CompositeWidget.glade")
            );
            BindTemplateChild2(gClass, nameof(Button));
            OnConnectEvent(gClass, type);
        }

        protected override void Initialize()
        {
            InitTemplate2();
            BindTemplateChild2(nameof(Button), ref Button);
        }

        private Button Button = default!;

        public CompositeWidget()
        {
            
        }

        private static void button_clicked(Button sender, System.EventArgs args)
        {
            sender.Label = "Clicked!";
        }
    }
}
