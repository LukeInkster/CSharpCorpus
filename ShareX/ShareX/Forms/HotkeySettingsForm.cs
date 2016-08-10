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
using ShareX.Properties;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ShareX
{
    public partial class HotkeySettingsForm : Form
    {
        public HotkeySelectionControl Selected { get; private set; }

        private HotkeyManager manager;

        public HotkeySettingsForm(HotkeyManager hotkeyManager)
        {
            InitializeComponent();
            Icon = ShareXResources.Icon;
            PrepareHotkeys(hotkeyManager);
        }

        private void HotkeySettingsForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (manager != null)
            {
                manager.IgnoreHotkeys = false;
                manager.HotkeysToggledTrigger -= HandleHotkeysToggle;
            }
        }

        public void PrepareHotkeys(HotkeyManager hotkeyManager)
        {
            if (manager == null)
            {
                manager = hotkeyManager;
                manager.HotkeysToggledTrigger += HandleHotkeysToggle;

                AddControls();
            }
        }

        private void AddControls()
        {
            flpHotkeys.Controls.Clear();

            foreach (HotkeySettings hotkeySetting in manager.Hotkeys)
            {
                AddHotkeySelectionControl(hotkeySetting);
            }
        }

        private void UpdateButtons()
        {
            btnEdit.Enabled = btnRemove.Enabled = btnDuplicate.Enabled = Selected != null;
            btnMoveUp.Enabled = btnMoveDown.Enabled = Selected != null && flpHotkeys.Controls.Count > 1;
        }

        private HotkeySelectionControl FindSelectionControl(HotkeySettings hotkeySetting)
        {
            return flpHotkeys.Controls.Cast<HotkeySelectionControl>().FirstOrDefault(hsc => hsc.Setting == hotkeySetting);
        }

        private void control_SelectedChanged(object sender, EventArgs e)
        {
            Selected = (HotkeySelectionControl)sender;
            UpdateButtons();
            UpdateCheckStates();
        }

        private void UpdateCheckStates()
        {
            foreach (Control control in flpHotkeys.Controls)
            {
                ((HotkeySelectionControl)control).Selected = Selected == control;
            }
        }

        private void UpdateHotkeyStatus()
        {
            foreach (Control control in flpHotkeys.Controls)
            {
                ((HotkeySelectionControl)control).UpdateHotkeyStatus();
            }
        }

        private void control_HotkeyChanged(object sender, EventArgs e)
        {
            HotkeySelectionControl control = (HotkeySelectionControl)sender;
            manager.RegisterHotkey(control.Setting);
        }

        private HotkeySelectionControl AddHotkeySelectionControl(HotkeySettings hotkeySetting)
        {
            HotkeySelectionControl control = new HotkeySelectionControl(hotkeySetting);
            control.Margin = new Padding(0, 0, 0, 2);
            control.SelectedChanged += control_SelectedChanged;
            control.HotkeyChanged += control_HotkeyChanged;
            control.EditRequested += control_EditRequested;
            flpHotkeys.Controls.Add(control);
            return control;
        }

        private void Edit(HotkeySelectionControl selectionControl)
        {
            using (TaskSettingsForm taskSettingsForm = new TaskSettingsForm(selectionControl.Setting.TaskSettings))
            {
                taskSettingsForm.ShowDialog();
                selectionControl.UpdateDescription();
            }
        }

        private void control_EditRequested(object sender, EventArgs e)
        {
            Edit((HotkeySelectionControl)sender);
        }

        private void EditSelected()
        {
            if (Selected != null)
            {
                Edit(Selected);
            }
        }

        private void HandleHotkeysToggle(bool hotkeysEnabled)
        {
            UpdateHotkeyStatus();
        }

        private void flpHotkeys_Layout(object sender, LayoutEventArgs e)
        {
            foreach (Control control in flpHotkeys.Controls)
            {
                control.ClientSize = new Size(flpHotkeys.ClientSize.Width, control.ClientSize.Height);
            }
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            HotkeySettings hotkeySetting = new HotkeySettings();
            hotkeySetting.TaskSettings = TaskSettings.GetDefaultTaskSettings();
            manager.Hotkeys.Add(hotkeySetting);
            HotkeySelectionControl control = AddHotkeySelectionControl(hotkeySetting);
            control.Selected = true;
            Selected = control;
            UpdateButtons();
            UpdateCheckStates();
            control.Focus();
            Update();
            EditSelected();
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            if (Selected != null)
            {
                manager.UnregisterHotkey(Selected.Setting);
                HotkeySelectionControl hsc = FindSelectionControl(Selected.Setting);
                if (hsc != null) flpHotkeys.Controls.Remove(hsc);
                Selected = null;
                UpdateButtons();
            }
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            EditSelected();
        }

        private void btnDuplicate_Click(object sender, EventArgs e)
        {
            if (Selected != null)
            {
                HotkeySettings hotkeySetting = new HotkeySettings();
                hotkeySetting.TaskSettings = Selected.Setting.TaskSettings.Copy();
                hotkeySetting.TaskSettings.WatchFolderEnabled = false;
                hotkeySetting.TaskSettings.WatchFolderList = new List<WatchFolderSettings>();
                manager.Hotkeys.Add(hotkeySetting);
                HotkeySelectionControl control = AddHotkeySelectionControl(hotkeySetting);
                control.Selected = true;
                Selected = control;
                UpdateCheckStates();
                control.Focus();
            }
        }

        private void btnMoveUp_Click(object sender, EventArgs e)
        {
            if (Selected != null && flpHotkeys.Controls.Count > 1)
            {
                int index = flpHotkeys.Controls.GetChildIndex(Selected);

                int newIndex;

                if (index == 0)
                {
                    newIndex = flpHotkeys.Controls.Count - 1;
                }
                else
                {
                    newIndex = index - 1;
                }

                flpHotkeys.Controls.SetChildIndex(Selected, newIndex);
                manager.Hotkeys.Move(index, newIndex);
            }
        }

        private void btnMoveDown_Click(object sender, EventArgs e)
        {
            if (Selected != null && flpHotkeys.Controls.Count > 1)
            {
                int index = flpHotkeys.Controls.GetChildIndex(Selected);

                int newIndex;

                if (index == flpHotkeys.Controls.Count - 1)
                {
                    newIndex = 0;
                }
                else
                {
                    newIndex = index + 1;
                }

                flpHotkeys.Controls.SetChildIndex(Selected, newIndex);
                manager.Hotkeys.Move(index, newIndex);
            }
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(Resources.HotkeySettingsForm_Reset_all_hotkeys_to_defaults_Confirmation, "ShareX", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                manager.ResetHotkeys();
                AddControls();
                Selected = null;
                UpdateButtons();
            }
        }
    }
}