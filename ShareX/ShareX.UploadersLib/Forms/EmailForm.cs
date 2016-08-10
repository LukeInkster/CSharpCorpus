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

using ShareX.HelpersLib;
using System;
using System.Windows.Forms;

namespace ShareX.UploadersLib
{
    public partial class EmailForm : Form
    {
        public string ToEmail { get; private set; }
        public string Subject { get; private set; }
        public string Body { get; private set; }

        public EmailForm()
        {
            InitializeComponent();
            Icon = ShareXResources.Icon;
        }

        public EmailForm(string toEmail, string subject, string body) : this()
        {
            txtToEmail.Text = toEmail;
            txtSubject.Text = subject;
            txtMessage.Text = body;
        }

        private void EmailForm_Shown(object sender, EventArgs e)
        {
            txtMessage.Focus();
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            ToEmail = txtToEmail.Text;
            Subject = txtSubject.Text;
            Body = txtMessage.Text;

            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}