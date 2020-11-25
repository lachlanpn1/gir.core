﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using GObject;

namespace Gst
{
    public partial class Bin
    {
        public Bin(string name) : this(ConstructParameter.With(NameProperty, name)) { }

        public bool Add(Element element) => Native.add(Handle, GetHandle(element));
        public bool Remove(Element element) => Native.remove(Handle, GetHandle(element));

        // FIXME: Bulletproof
        public IEnumerable<Element> IterateRecurse()
        {
            IntPtr ptr = Native.iterate_recurse(Handle);
            foreach (Value val in Marshal.PtrToStructure<Iterator>(ptr).GetValues())
            {
                Element el = val.Extract<Element>();
                val.Dispose();
                yield return el;
            }
        }
    }
}
