﻿#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2016 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using System;
using System.Windows.Forms;

namespace ShareX.HelpersLib
{
    public partial class LabeledNumericUpDown : UserControl
    {
        public new string Text
        {
            get
            {
                return lblText.Text;
            }
            set
            {
                lblText.Text = value;
            }
        }

        public decimal Value
        {
            get
            {
                return nudValue.Value;
            }
            set
            {
                nudValue.SetValue(value);
            }
        }

        public decimal Maximum
        {
            get
            {
                return nudValue.Maximum;
            }
            set
            {
                nudValue.Maximum = value;
            }
        }

        public decimal Minimum
        {
            get
            {
                return nudValue.Minimum;
            }
            set
            {
                nudValue.Minimum = value;
            }
        }

        public decimal Increment
        {
            get
            {
                return nudValue.Increment;
            }
            set
            {
                nudValue.Increment = value;
            }
        }

        public EventHandler ValueChanged;

        public LabeledNumericUpDown()
        {
            InitializeComponent();
            nudValue.ValueChanged += OnValueChanged;
        }

        private void OnValueChanged(object sender, EventArgs e)
        {
            if (ValueChanged != null)
            {
                ValueChanged(sender, e);
            }
        }
    }
}