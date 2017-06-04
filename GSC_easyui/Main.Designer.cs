namespace GSC_easyui
{
	partial class Main
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
			this.statusStrip1 = new System.Windows.Forms.StatusStrip();
			this.menuStrip1 = new System.Windows.Forms.MenuStrip();
			this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.saveFileLocationsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.loadFileLocationsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
			this.addNewFilesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.addNewFoldersToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.splitContainer1 = new System.Windows.Forms.SplitContainer();
			this.tabControl1 = new System.Windows.Forms.TabControl();
			this.tabPage1 = new System.Windows.Forms.TabPage();
			this.tabPage2 = new System.Windows.Forms.TabPage();
			this.compilerOptionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.configureGlobalOptionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.configureInterfaceOptionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.configureWorkingSetToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.menuStrip1.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
			this.splitContainer1.Panel1.SuspendLayout();
			this.splitContainer1.SuspendLayout();
			this.tabControl1.SuspendLayout();
			this.SuspendLayout();
			// 
			// statusStrip1
			// 
			this.statusStrip1.Location = new System.Drawing.Point(0, 466);
			this.statusStrip1.Name = "statusStrip1";
			this.statusStrip1.Size = new System.Drawing.Size(629, 22);
			this.statusStrip1.TabIndex = 1;
			this.statusStrip1.Text = "statusStrip1";
			// 
			// menuStrip1
			// 
			this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.compilerOptionsToolStripMenuItem,
            this.configureWorkingSetToolStripMenuItem});
			this.menuStrip1.Location = new System.Drawing.Point(0, 0);
			this.menuStrip1.Name = "menuStrip1";
			this.menuStrip1.Size = new System.Drawing.Size(629, 24);
			this.menuStrip1.TabIndex = 2;
			this.menuStrip1.Text = "menuStrip1";
			// 
			// fileToolStripMenuItem
			// 
			this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.saveFileLocationsToolStripMenuItem,
            this.loadFileLocationsToolStripMenuItem,
            this.toolStripSeparator1,
            this.addNewFilesToolStripMenuItem,
            this.addNewFoldersToolStripMenuItem});
			this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
			this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
			this.fileToolStripMenuItem.Text = "File";
			// 
			// saveFileLocationsToolStripMenuItem
			// 
			this.saveFileLocationsToolStripMenuItem.Name = "saveFileLocationsToolStripMenuItem";
			this.saveFileLocationsToolStripMenuItem.Size = new System.Drawing.Size(182, 22);
			this.saveFileLocationsToolStripMenuItem.Text = "Save File Locations...";
			// 
			// loadFileLocationsToolStripMenuItem
			// 
			this.loadFileLocationsToolStripMenuItem.Name = "loadFileLocationsToolStripMenuItem";
			this.loadFileLocationsToolStripMenuItem.Size = new System.Drawing.Size(184, 22);
			this.loadFileLocationsToolStripMenuItem.Text = "Load File Locations...";
			this.loadFileLocationsToolStripMenuItem.Click += new System.EventHandler(this.loadFileLocationsToolStripMenuItem_Click);
			// 
			// toolStripSeparator1
			// 
			this.toolStripSeparator1.Name = "toolStripSeparator1";
			this.toolStripSeparator1.Size = new System.Drawing.Size(181, 6);
			// 
			// addNewFilesToolStripMenuItem
			// 
			this.addNewFilesToolStripMenuItem.Name = "addNewFilesToolStripMenuItem";
			this.addNewFilesToolStripMenuItem.Size = new System.Drawing.Size(184, 22);
			this.addNewFilesToolStripMenuItem.Text = "Add New Files...";
			// 
			// addNewFoldersToolStripMenuItem
			// 
			this.addNewFoldersToolStripMenuItem.Name = "addNewFoldersToolStripMenuItem";
			this.addNewFoldersToolStripMenuItem.Size = new System.Drawing.Size(184, 22);
			this.addNewFoldersToolStripMenuItem.Text = "Add New Folders...";
			// 
			// splitContainer1
			// 
			this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.splitContainer1.Location = new System.Drawing.Point(0, 24);
			this.splitContainer1.Name = "splitContainer1";
			this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
			// 
			// splitContainer1.Panel1
			// 
			this.splitContainer1.Panel1.Controls.Add(this.tabControl1);
			this.splitContainer1.Size = new System.Drawing.Size(629, 442);
			this.splitContainer1.SplitterDistance = 329;
			this.splitContainer1.TabIndex = 3;
			// 
			// tabControl1
			// 
			this.tabControl1.Controls.Add(this.tabPage1);
			this.tabControl1.Controls.Add(this.tabPage2);
			this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.tabControl1.Location = new System.Drawing.Point(0, 0);
			this.tabControl1.Name = "tabControl1";
			this.tabControl1.SelectedIndex = 0;
			this.tabControl1.Size = new System.Drawing.Size(629, 329);
			this.tabControl1.TabIndex = 0;
			// 
			// tabPage1
			// 
			this.tabPage1.Location = new System.Drawing.Point(4, 22);
			this.tabPage1.Name = "tabPage1";
			this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage1.Size = new System.Drawing.Size(621, 303);
			this.tabPage1.TabIndex = 0;
			this.tabPage1.Text = "tabPage1";
			this.tabPage1.UseVisualStyleBackColor = true;
			// 
			// tabPage2
			// 
			this.tabPage2.Location = new System.Drawing.Point(4, 22);
			this.tabPage2.Name = "tabPage2";
			this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage2.Size = new System.Drawing.Size(192, 74);
			this.tabPage2.TabIndex = 1;
			this.tabPage2.Text = "tabPage2";
			this.tabPage2.UseVisualStyleBackColor = true;
			// 
			// compilerOptionsToolStripMenuItem
			// 
			this.compilerOptionsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.configureGlobalOptionsToolStripMenuItem,
            this.configureInterfaceOptionsToolStripMenuItem});
			this.compilerOptionsToolStripMenuItem.Name = "compilerOptionsToolStripMenuItem";
			this.compilerOptionsToolStripMenuItem.Size = new System.Drawing.Size(113, 20);
			this.compilerOptionsToolStripMenuItem.Text = "Compiler Options";
			// 
			// configureGlobalOptionsToolStripMenuItem
			// 
			this.configureGlobalOptionsToolStripMenuItem.Name = "configureGlobalOptionsToolStripMenuItem";
			this.configureGlobalOptionsToolStripMenuItem.Size = new System.Drawing.Size(221, 22);
			this.configureGlobalOptionsToolStripMenuItem.Text = "Configure Global Options";
			// 
			// configureInterfaceOptionsToolStripMenuItem
			// 
			this.configureInterfaceOptionsToolStripMenuItem.Name = "configureInterfaceOptionsToolStripMenuItem";
			this.configureInterfaceOptionsToolStripMenuItem.Size = new System.Drawing.Size(221, 22);
			this.configureInterfaceOptionsToolStripMenuItem.Text = "Configure Interface Options";
			// 
			// configureWorkingSetToolStripMenuItem
			// 
			this.configureWorkingSetToolStripMenuItem.Name = "configureWorkingSetToolStripMenuItem";
			this.configureWorkingSetToolStripMenuItem.Size = new System.Drawing.Size(139, 20);
			this.configureWorkingSetToolStripMenuItem.Text = "Configure Working Set";
			// 
			// Main
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(629, 488);
			this.Controls.Add(this.splitContainer1);
			this.Controls.Add(this.statusStrip1);
			this.Controls.Add(this.menuStrip1);
			this.MainMenuStrip = this.menuStrip1;
			this.Name = "Main";
			this.Text = "Form1";
			this.menuStrip1.ResumeLayout(false);
			this.menuStrip1.PerformLayout();
			this.splitContainer1.Panel1.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
			this.splitContainer1.ResumeLayout(false);
			this.tabControl1.ResumeLayout(false);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.StatusStrip statusStrip1;
		private System.Windows.Forms.MenuStrip menuStrip1;
		private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem saveFileLocationsToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem loadFileLocationsToolStripMenuItem;
		private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
		private System.Windows.Forms.ToolStripMenuItem addNewFilesToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem addNewFoldersToolStripMenuItem;
		private System.Windows.Forms.SplitContainer splitContainer1;
		private System.Windows.Forms.TabControl tabControl1;
		private System.Windows.Forms.TabPage tabPage1;
		private System.Windows.Forms.TabPage tabPage2;
		private System.Windows.Forms.ToolStripMenuItem compilerOptionsToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem configureGlobalOptionsToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem configureInterfaceOptionsToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem configureWorkingSetToolStripMenuItem;

	}
}

