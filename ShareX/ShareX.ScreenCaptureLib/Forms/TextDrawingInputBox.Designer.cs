﻿namespace ShareX.ScreenCaptureLib
{
    partial class TextDrawingInputBox
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(TextDrawingInputBox));
            this.txtInput = new System.Windows.Forms.TextBox();
            this.btnTextColor = new ShareX.HelpersLib.ColorButton();
            this.flpProperties = new System.Windows.Forms.FlowLayoutPanel();
            this.lblFont = new System.Windows.Forms.Label();
            this.cbFonts = new System.Windows.Forms.ComboBox();
            this.lblTextSize = new System.Windows.Forms.Label();
            this.nudTextSize = new System.Windows.Forms.NumericUpDown();
            this.cbBold = new System.Windows.Forms.CheckBox();
            this.cbItalic = new System.Windows.Forms.CheckBox();
            this.cbUnderline = new System.Windows.Forms.CheckBox();
            this.btnAlignmentHorizontal = new System.Windows.Forms.Button();
            this.btnAlignmentVertical = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            this.cmsAlignmentHorizontal = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.tsmiAlignmentLeft = new System.Windows.Forms.ToolStripMenuItem();
            this.tsmiAlignmentCenter = new System.Windows.Forms.ToolStripMenuItem();
            this.tsmiAlignmentRight = new System.Windows.Forms.ToolStripMenuItem();
            this.cmsAlignmentVertical = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.tsmiAlignmentTop = new System.Windows.Forms.ToolStripMenuItem();
            this.tsmiAlignmentMiddle = new System.Windows.Forms.ToolStripMenuItem();
            this.tsmiAlignmentBottom = new System.Windows.Forms.ToolStripMenuItem();
            this.flpProperties.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nudTextSize)).BeginInit();
            this.cmsAlignmentHorizontal.SuspendLayout();
            this.cmsAlignmentVertical.SuspendLayout();
            this.SuspendLayout();
            // 
            // txtInput
            // 
            resources.ApplyResources(this.txtInput, "txtInput");
            this.txtInput.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtInput.Name = "txtInput";
            this.txtInput.TextChanged += new System.EventHandler(this.txtInput_TextChanged);
            // 
            // btnTextColor
            // 
            resources.ApplyResources(this.btnTextColor, "btnTextColor");
            this.btnTextColor.Color = System.Drawing.Color.Empty;
            this.btnTextColor.Name = "btnTextColor";
            this.btnTextColor.UseVisualStyleBackColor = true;
            this.btnTextColor.ColorChanged += new ShareX.HelpersLib.ColorButton.ColorChangedEventHandler(this.btnTextColor_ColorChanged);
            // 
            // flpProperties
            // 
            resources.ApplyResources(this.flpProperties, "flpProperties");
            this.flpProperties.Controls.Add(this.lblFont);
            this.flpProperties.Controls.Add(this.cbFonts);
            this.flpProperties.Controls.Add(this.lblTextSize);
            this.flpProperties.Controls.Add(this.nudTextSize);
            this.flpProperties.Controls.Add(this.btnTextColor);
            this.flpProperties.Controls.Add(this.cbBold);
            this.flpProperties.Controls.Add(this.cbItalic);
            this.flpProperties.Controls.Add(this.cbUnderline);
            this.flpProperties.Controls.Add(this.btnAlignmentHorizontal);
            this.flpProperties.Controls.Add(this.btnAlignmentVertical);
            this.flpProperties.Name = "flpProperties";
            // 
            // lblFont
            // 
            resources.ApplyResources(this.lblFont, "lblFont");
            this.lblFont.Name = "lblFont";
            // 
            // cbFonts
            // 
            resources.ApplyResources(this.cbFonts, "cbFonts");
            this.cbFonts.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbFonts.FormattingEnabled = true;
            this.cbFonts.Name = "cbFonts";
            this.cbFonts.SelectedIndexChanged += new System.EventHandler(this.cbFonts_SelectedIndexChanged);
            // 
            // lblTextSize
            // 
            resources.ApplyResources(this.lblTextSize, "lblTextSize");
            this.lblTextSize.Name = "lblTextSize";
            // 
            // nudTextSize
            // 
            resources.ApplyResources(this.nudTextSize, "nudTextSize");
            this.nudTextSize.Minimum = new decimal(new int[] {
            5,
            0,
            0,
            0});
            this.nudTextSize.Name = "nudTextSize";
            this.nudTextSize.Value = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.nudTextSize.ValueChanged += new System.EventHandler(this.nudTextSize_ValueChanged);
            // 
            // cbBold
            // 
            resources.ApplyResources(this.cbBold, "cbBold");
            this.cbBold.Image = global::ShareX.ScreenCaptureLib.Properties.Resources.edit_bold;
            this.cbBold.Name = "cbBold";
            this.cbBold.UseVisualStyleBackColor = true;
            this.cbBold.CheckedChanged += new System.EventHandler(this.cbBold_CheckedChanged);
            // 
            // cbItalic
            // 
            resources.ApplyResources(this.cbItalic, "cbItalic");
            this.cbItalic.Image = global::ShareX.ScreenCaptureLib.Properties.Resources.edit_italic;
            this.cbItalic.Name = "cbItalic";
            this.cbItalic.UseVisualStyleBackColor = true;
            this.cbItalic.CheckedChanged += new System.EventHandler(this.cbItalic_CheckedChanged);
            // 
            // cbUnderline
            // 
            resources.ApplyResources(this.cbUnderline, "cbUnderline");
            this.cbUnderline.Image = global::ShareX.ScreenCaptureLib.Properties.Resources.edit_underline;
            this.cbUnderline.Name = "cbUnderline";
            this.cbUnderline.UseVisualStyleBackColor = true;
            this.cbUnderline.CheckedChanged += new System.EventHandler(this.cbUnderline_CheckedChanged);
            // 
            // btnAlignmentHorizontal
            // 
            resources.ApplyResources(this.btnAlignmentHorizontal, "btnAlignmentHorizontal");
            this.btnAlignmentHorizontal.Image = global::ShareX.ScreenCaptureLib.Properties.Resources.edit_alignment_center;
            this.btnAlignmentHorizontal.Name = "btnAlignmentHorizontal";
            this.btnAlignmentHorizontal.UseVisualStyleBackColor = true;
            this.btnAlignmentHorizontal.Click += new System.EventHandler(this.btnAlignmentHorizontal_Click);
            // 
            // btnAlignmentVertical
            // 
            resources.ApplyResources(this.btnAlignmentVertical, "btnAlignmentVertical");
            this.btnAlignmentVertical.Image = global::ShareX.ScreenCaptureLib.Properties.Resources.edit_vertical_alignment_middle;
            this.btnAlignmentVertical.Name = "btnAlignmentVertical";
            this.btnAlignmentVertical.UseVisualStyleBackColor = true;
            this.btnAlignmentVertical.Click += new System.EventHandler(this.btnAlignmentVertical_Click);
            // 
            // btnClose
            // 
            resources.ApplyResources(this.btnClose, "btnClose");
            this.btnClose.Name = "btnClose";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // cmsAlignmentHorizontal
            // 
            this.cmsAlignmentHorizontal.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tsmiAlignmentLeft,
            this.tsmiAlignmentCenter,
            this.tsmiAlignmentRight});
            this.cmsAlignmentHorizontal.Name = "cmsAlignmentHorizontal";
            resources.ApplyResources(this.cmsAlignmentHorizontal, "cmsAlignmentHorizontal");
            // 
            // tsmiAlignmentLeft
            // 
            this.tsmiAlignmentLeft.Image = global::ShareX.ScreenCaptureLib.Properties.Resources.edit_alignment;
            this.tsmiAlignmentLeft.Name = "tsmiAlignmentLeft";
            resources.ApplyResources(this.tsmiAlignmentLeft, "tsmiAlignmentLeft");
            this.tsmiAlignmentLeft.Click += new System.EventHandler(this.tsmiAlignmentLeft_Click);
            // 
            // tsmiAlignmentCenter
            // 
            this.tsmiAlignmentCenter.Image = global::ShareX.ScreenCaptureLib.Properties.Resources.edit_alignment_center;
            this.tsmiAlignmentCenter.Name = "tsmiAlignmentCenter";
            resources.ApplyResources(this.tsmiAlignmentCenter, "tsmiAlignmentCenter");
            this.tsmiAlignmentCenter.Click += new System.EventHandler(this.tsmiAlignmentCenter_Click);
            // 
            // tsmiAlignmentRight
            // 
            this.tsmiAlignmentRight.Image = global::ShareX.ScreenCaptureLib.Properties.Resources.edit_alignment_right;
            this.tsmiAlignmentRight.Name = "tsmiAlignmentRight";
            resources.ApplyResources(this.tsmiAlignmentRight, "tsmiAlignmentRight");
            this.tsmiAlignmentRight.Click += new System.EventHandler(this.tsmiAlignmentRight_Click);
            // 
            // cmsAlignmentVertical
            // 
            this.cmsAlignmentVertical.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tsmiAlignmentTop,
            this.tsmiAlignmentMiddle,
            this.tsmiAlignmentBottom});
            this.cmsAlignmentVertical.Name = "cmsAlignmentVertical";
            resources.ApplyResources(this.cmsAlignmentVertical, "cmsAlignmentVertical");
            // 
            // tsmiAlignmentTop
            // 
            this.tsmiAlignmentTop.Image = global::ShareX.ScreenCaptureLib.Properties.Resources.edit_vertical_alignment_top;
            this.tsmiAlignmentTop.Name = "tsmiAlignmentTop";
            resources.ApplyResources(this.tsmiAlignmentTop, "tsmiAlignmentTop");
            this.tsmiAlignmentTop.Click += new System.EventHandler(this.tsmiAlignmentTop_Click);
            // 
            // tsmiAlignmentMiddle
            // 
            this.tsmiAlignmentMiddle.Image = global::ShareX.ScreenCaptureLib.Properties.Resources.edit_vertical_alignment_middle;
            this.tsmiAlignmentMiddle.Name = "tsmiAlignmentMiddle";
            resources.ApplyResources(this.tsmiAlignmentMiddle, "tsmiAlignmentMiddle");
            this.tsmiAlignmentMiddle.Click += new System.EventHandler(this.tsmiAlignmentMiddle_Click);
            // 
            // tsmiAlignmentBottom
            // 
            this.tsmiAlignmentBottom.Image = global::ShareX.ScreenCaptureLib.Properties.Resources.edit_vertical_alignment;
            this.tsmiAlignmentBottom.Name = "tsmiAlignmentBottom";
            resources.ApplyResources(this.tsmiAlignmentBottom, "tsmiAlignmentBottom");
            this.tsmiAlignmentBottom.Click += new System.EventHandler(this.tsmiAlignmentBottom_Click);
            // 
            // TextDrawingInputBox
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.flpProperties);
            this.Controls.Add(this.txtInput);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "TextDrawingInputBox";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.TopMost = true;
            this.Shown += new System.EventHandler(this.TextDrawingInputBox_Shown);
            this.flpProperties.ResumeLayout(false);
            this.flpProperties.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nudTextSize)).EndInit();
            this.cmsAlignmentHorizontal.ResumeLayout(false);
            this.cmsAlignmentVertical.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox txtInput;
        private HelpersLib.ColorButton btnTextColor;
        private System.Windows.Forms.FlowLayoutPanel flpProperties;
        private System.Windows.Forms.Label lblTextSize;
        private System.Windows.Forms.NumericUpDown nudTextSize;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.CheckBox cbBold;
        private System.Windows.Forms.CheckBox cbItalic;
        private System.Windows.Forms.CheckBox cbUnderline;
        private System.Windows.Forms.Label lblFont;
        private System.Windows.Forms.ComboBox cbFonts;
        private System.Windows.Forms.Button btnAlignmentHorizontal;
        private System.Windows.Forms.Button btnAlignmentVertical;
        private System.Windows.Forms.ContextMenuStrip cmsAlignmentHorizontal;
        private System.Windows.Forms.ContextMenuStrip cmsAlignmentVertical;
        private System.Windows.Forms.ToolStripMenuItem tsmiAlignmentLeft;
        private System.Windows.Forms.ToolStripMenuItem tsmiAlignmentCenter;
        private System.Windows.Forms.ToolStripMenuItem tsmiAlignmentRight;
        private System.Windows.Forms.ToolStripMenuItem tsmiAlignmentTop;
        private System.Windows.Forms.ToolStripMenuItem tsmiAlignmentMiddle;
        private System.Windows.Forms.ToolStripMenuItem tsmiAlignmentBottom;
    }
}