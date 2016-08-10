﻿namespace ShareX.HelpersLib
{
    partial class DebugForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DebugForm));
            this.btnLoadedAssemblies = new System.Windows.Forms.Button();
            this.btnCopyAll = new System.Windows.Forms.Button();
            this.rtbDebug = new System.Windows.Forms.RichTextBox();
            this.btnOpenLogFile = new System.Windows.Forms.Button();
            this.llRunningFrom = new System.Windows.Forms.LinkLabel();
            this.flpRunningFrom = new System.Windows.Forms.FlowLayoutPanel();
            this.lblRunningFrom = new System.Windows.Forms.Label();
            this.flpRunningFrom.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnLoadedAssemblies
            // 
            resources.ApplyResources(this.btnLoadedAssemblies, "btnLoadedAssemblies");
            this.btnLoadedAssemblies.Name = "btnLoadedAssemblies";
            this.btnLoadedAssemblies.UseVisualStyleBackColor = true;
            this.btnLoadedAssemblies.Click += new System.EventHandler(this.btnLoadedAssemblies_Click);
            // 
            // btnCopyAll
            // 
            resources.ApplyResources(this.btnCopyAll, "btnCopyAll");
            this.btnCopyAll.Name = "btnCopyAll";
            this.btnCopyAll.UseVisualStyleBackColor = true;
            this.btnCopyAll.Click += new System.EventHandler(this.btnCopyAll_Click);
            // 
            // rtbDebug
            // 
            resources.ApplyResources(this.rtbDebug, "rtbDebug");
            this.rtbDebug.BackColor = System.Drawing.SystemColors.Window;
            this.rtbDebug.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.rtbDebug.Name = "rtbDebug";
            this.rtbDebug.ReadOnly = true;
            this.rtbDebug.LinkClicked += new System.Windows.Forms.LinkClickedEventHandler(this.rtbDebug_LinkClicked);
            // 
            // btnOpenLogFile
            // 
            resources.ApplyResources(this.btnOpenLogFile, "btnOpenLogFile");
            this.btnOpenLogFile.Name = "btnOpenLogFile";
            this.btnOpenLogFile.UseVisualStyleBackColor = true;
            this.btnOpenLogFile.Click += new System.EventHandler(this.btnOpenLogFile_Click);
            // 
            // llRunningFrom
            // 
            resources.ApplyResources(this.llRunningFrom, "llRunningFrom");
            this.llRunningFrom.LinkBehavior = System.Windows.Forms.LinkBehavior.NeverUnderline;
            this.llRunningFrom.Name = "llRunningFrom";
            // 
            // flpRunningFrom
            // 
            resources.ApplyResources(this.flpRunningFrom, "flpRunningFrom");
            this.flpRunningFrom.Controls.Add(this.lblRunningFrom);
            this.flpRunningFrom.Controls.Add(this.llRunningFrom);
            this.flpRunningFrom.Name = "flpRunningFrom";
            // 
            // lblRunningFrom
            // 
            resources.ApplyResources(this.lblRunningFrom, "lblRunningFrom");
            this.lblRunningFrom.Name = "lblRunningFrom";
            // 
            // DebugForm
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.Controls.Add(this.flpRunningFrom);
            this.Controls.Add(this.btnOpenLogFile);
            this.Controls.Add(this.rtbDebug);
            this.Controls.Add(this.btnCopyAll);
            this.Controls.Add(this.btnLoadedAssemblies);
            this.Name = "DebugForm";
            this.flpRunningFrom.ResumeLayout(false);
            this.flpRunningFrom.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnLoadedAssemblies;
        private System.Windows.Forms.Button btnCopyAll;
        private System.Windows.Forms.RichTextBox rtbDebug;
        private System.Windows.Forms.Button btnOpenLogFile;
        private System.Windows.Forms.LinkLabel llRunningFrom;
        private System.Windows.Forms.FlowLayoutPanel flpRunningFrom;
        private System.Windows.Forms.Label lblRunningFrom;
    }
}