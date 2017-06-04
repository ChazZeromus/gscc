namespace GscCodeTest
{
	partial class MainForm
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
			System.Windows.Forms.ListViewItem listViewItem3 = new System.Windows.Forms.ListViewItem("Test Compiler");
			this.splitContainer1 = new System.Windows.Forms.SplitContainer();
			this.splitContainer2 = new System.Windows.Forms.SplitContainer();
			this.splitContainer3 = new System.Windows.Forms.SplitContainer();
			this.button2 = new System.Windows.Forms.Button();
			this.treeView1 = new System.Windows.Forms.TreeView();
			this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
			this.openToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.createFolderToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.createSnippetToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.renameToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.removeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.richTextBoxInput = new System.Windows.Forms.RichTextBox();
			this.richTextBoxOutput = new System.Windows.Forms.RichTextBox();
			this.buttonPack = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.buttonGraph = new System.Windows.Forms.Button();
			this.listViewLog = new System.Windows.Forms.ListView();
			this.label1 = new System.Windows.Forms.Label();
			this.comboBoxScope = new System.Windows.Forms.ComboBox();
			this.buttonOptions = new System.Windows.Forms.Button();
			this.buttonCrossCheck = new System.Windows.Forms.Button();
			this.buttonEval = new System.Windows.Forms.Button();
			this.buttonCompile = new System.Windows.Forms.Button();
			((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
			this.splitContainer1.Panel1.SuspendLayout();
			this.splitContainer1.Panel2.SuspendLayout();
			this.splitContainer1.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).BeginInit();
			this.splitContainer2.Panel1.SuspendLayout();
			this.splitContainer2.Panel2.SuspendLayout();
			this.splitContainer2.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.splitContainer3)).BeginInit();
			this.splitContainer3.Panel1.SuspendLayout();
			this.splitContainer3.Panel2.SuspendLayout();
			this.splitContainer3.SuspendLayout();
			this.contextMenuStrip1.SuspendLayout();
			this.SuspendLayout();
			// 
			// splitContainer1
			// 
			this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.splitContainer1.Location = new System.Drawing.Point(0, 0);
			this.splitContainer1.Name = "splitContainer1";
			this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
			// 
			// splitContainer1.Panel1
			// 
			this.splitContainer1.Panel1.Controls.Add(this.splitContainer2);
			// 
			// splitContainer1.Panel2
			// 
			this.splitContainer1.Panel2.Controls.Add(this.buttonPack);
			this.splitContainer1.Panel2.Controls.Add(this.button1);
			this.splitContainer1.Panel2.Controls.Add(this.buttonGraph);
			this.splitContainer1.Panel2.Controls.Add(this.listViewLog);
			this.splitContainer1.Panel2.Controls.Add(this.label1);
			this.splitContainer1.Panel2.Controls.Add(this.comboBoxScope);
			this.splitContainer1.Panel2.Controls.Add(this.buttonOptions);
			this.splitContainer1.Panel2.Controls.Add(this.buttonCrossCheck);
			this.splitContainer1.Panel2.Controls.Add(this.buttonEval);
			this.splitContainer1.Panel2.Controls.Add(this.buttonCompile);
			this.splitContainer1.Size = new System.Drawing.Size(996, 494);
			this.splitContainer1.SplitterDistance = 351;
			this.splitContainer1.TabIndex = 0;
			// 
			// splitContainer2
			// 
			this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
			this.splitContainer2.Location = new System.Drawing.Point(0, 0);
			this.splitContainer2.Name = "splitContainer2";
			// 
			// splitContainer2.Panel1
			// 
			this.splitContainer2.Panel1.Controls.Add(this.splitContainer3);
			// 
			// splitContainer2.Panel2
			// 
			this.splitContainer2.Panel2.Controls.Add(this.richTextBoxOutput);
			this.splitContainer2.Size = new System.Drawing.Size(996, 351);
			this.splitContainer2.SplitterDistance = 630;
			this.splitContainer2.TabIndex = 0;
			// 
			// splitContainer3
			// 
			this.splitContainer3.Dock = System.Windows.Forms.DockStyle.Fill;
			this.splitContainer3.Location = new System.Drawing.Point(0, 0);
			this.splitContainer3.Name = "splitContainer3";
			// 
			// splitContainer3.Panel1
			// 
			this.splitContainer3.Panel1.Controls.Add(this.button2);
			this.splitContainer3.Panel1.Controls.Add(this.treeView1);
			// 
			// splitContainer3.Panel2
			// 
			this.splitContainer3.Panel2.Controls.Add(this.richTextBoxInput);
			this.splitContainer3.Size = new System.Drawing.Size(630, 351);
			this.splitContainer3.SplitterDistance = 159;
			this.splitContainer3.TabIndex = 1;
			// 
			// button2
			// 
			this.button2.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
			this.button2.Location = new System.Drawing.Point(41, 328);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(75, 20);
			this.button2.TabIndex = 1;
			this.button2.Text = "Close";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(this.button2_Click);
			// 
			// treeView1
			// 
			this.treeView1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.treeView1.ContextMenuStrip = this.contextMenuStrip1;
			this.treeView1.FullRowSelect = true;
			this.treeView1.LabelEdit = true;
			this.treeView1.Location = new System.Drawing.Point(3, 3);
			this.treeView1.Name = "treeView1";
			this.treeView1.Size = new System.Drawing.Size(154, 319);
			this.treeView1.TabIndex = 0;
			this.treeView1.AfterLabelEdit += new System.Windows.Forms.NodeLabelEditEventHandler(this.treeView1_AfterLabelEdit);
			this.treeView1.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.treeView1_AfterSelect);
			this.treeView1.MouseClick += new System.Windows.Forms.MouseEventHandler(this.treeView1_MouseClick);
			// 
			// contextMenuStrip1
			// 
			this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openToolStripMenuItem,
            this.createFolderToolStripMenuItem,
            this.createSnippetToolStripMenuItem,
            this.renameToolStripMenuItem,
            this.removeToolStripMenuItem});
			this.contextMenuStrip1.Name = "contextMenuStrip1";
			this.contextMenuStrip1.Size = new System.Drawing.Size(156, 114);
			this.contextMenuStrip1.Opening += new System.ComponentModel.CancelEventHandler(this.contextMenuStrip1_Opening);
			// 
			// openToolStripMenuItem
			// 
			this.openToolStripMenuItem.Name = "openToolStripMenuItem";
			this.openToolStripMenuItem.Size = new System.Drawing.Size(155, 22);
			this.openToolStripMenuItem.Text = "Open";
			this.openToolStripMenuItem.Click += new System.EventHandler(this.openToolStripMenuItem_Click);
			// 
			// createFolderToolStripMenuItem
			// 
			this.createFolderToolStripMenuItem.Name = "createFolderToolStripMenuItem";
			this.createFolderToolStripMenuItem.Size = new System.Drawing.Size(155, 22);
			this.createFolderToolStripMenuItem.Text = "Create Folder";
			this.createFolderToolStripMenuItem.Click += new System.EventHandler(this.createFolderToolStripMenuItem_Click);
			// 
			// createSnippetToolStripMenuItem
			// 
			this.createSnippetToolStripMenuItem.Name = "createSnippetToolStripMenuItem";
			this.createSnippetToolStripMenuItem.Size = new System.Drawing.Size(155, 22);
			this.createSnippetToolStripMenuItem.Text = "Save as Snippet";
			this.createSnippetToolStripMenuItem.Click += new System.EventHandler(this.createSnippetToolStripMenuItem_Click);
			// 
			// renameToolStripMenuItem
			// 
			this.renameToolStripMenuItem.Name = "renameToolStripMenuItem";
			this.renameToolStripMenuItem.Size = new System.Drawing.Size(155, 22);
			this.renameToolStripMenuItem.Text = "Rename";
			this.renameToolStripMenuItem.Click += new System.EventHandler(this.renameToolStripMenuItem_Click);
			// 
			// removeToolStripMenuItem
			// 
			this.removeToolStripMenuItem.Name = "removeToolStripMenuItem";
			this.removeToolStripMenuItem.Size = new System.Drawing.Size(155, 22);
			this.removeToolStripMenuItem.Text = "Remove";
			this.removeToolStripMenuItem.Click += new System.EventHandler(this.removeToolStripMenuItem_Click);
			// 
			// richTextBoxInput
			// 
			this.richTextBoxInput.DetectUrls = false;
			this.richTextBoxInput.Dock = System.Windows.Forms.DockStyle.Fill;
			this.richTextBoxInput.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.richTextBoxInput.Location = new System.Drawing.Point(0, 0);
			this.richTextBoxInput.Name = "richTextBoxInput";
			this.richTextBoxInput.Size = new System.Drawing.Size(467, 351);
			this.richTextBoxInput.TabIndex = 0;
			this.richTextBoxInput.Text = "";
			// 
			// richTextBoxOutput
			// 
			this.richTextBoxOutput.BackColor = System.Drawing.Color.Black;
			this.richTextBoxOutput.Dock = System.Windows.Forms.DockStyle.Fill;
			this.richTextBoxOutput.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(192)))), ((int)(((byte)(255)))), ((int)(((byte)(192)))));
			this.richTextBoxOutput.Location = new System.Drawing.Point(0, 0);
			this.richTextBoxOutput.Name = "richTextBoxOutput";
			this.richTextBoxOutput.ReadOnly = true;
			this.richTextBoxOutput.Size = new System.Drawing.Size(362, 351);
			this.richTextBoxOutput.TabIndex = 0;
			this.richTextBoxOutput.Text = "";
			// 
			// buttonPack
			// 
			this.buttonPack.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.buttonPack.Location = new System.Drawing.Point(226, 105);
			this.buttonPack.Name = "buttonPack";
			this.buttonPack.Size = new System.Drawing.Size(75, 23);
			this.buttonPack.TabIndex = 10;
			this.buttonPack.Text = "Pack";
			this.buttonPack.UseVisualStyleBackColor = true;
			this.buttonPack.Click += new System.EventHandler(this.buttonPack_Click);
			// 
			// button1
			// 
			this.button1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.button1.Location = new System.Drawing.Point(848, 32);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(145, 23);
			this.button1.TabIndex = 9;
			this.button1.Text = "Inline-Evaluator Options";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(this.button1_Click);
			// 
			// buttonGraph
			// 
			this.buttonGraph.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.buttonGraph.Location = new System.Drawing.Point(848, 61);
			this.buttonGraph.Name = "buttonGraph";
			this.buttonGraph.Size = new System.Drawing.Size(145, 23);
			this.buttonGraph.TabIndex = 8;
			this.buttonGraph.Text = "View Graph";
			this.buttonGraph.UseVisualStyleBackColor = true;
			this.buttonGraph.Click += new System.EventHandler(this.buttonGraph_Click);
			// 
			// listViewLog
			// 
			this.listViewLog.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.listViewLog.FullRowSelect = true;
			this.listViewLog.Items.AddRange(new System.Windows.Forms.ListViewItem[] {
            listViewItem3});
			this.listViewLog.Location = new System.Drawing.Point(12, 2);
			this.listViewLog.MultiSelect = false;
			this.listViewLog.Name = "listViewLog";
			this.listViewLog.Size = new System.Drawing.Size(830, 96);
			this.listViewLog.TabIndex = 7;
			this.listViewLog.TileSize = new System.Drawing.Size(200, 30);
			this.listViewLog.UseCompatibleStateImageBehavior = false;
			this.listViewLog.View = System.Windows.Forms.View.List;
			this.listViewLog.DoubleClick += new System.EventHandler(this.listViewLog_DoubleClick);
			// 
			// label1
			// 
			this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(310, 110);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(67, 13);
			this.label1.TabIndex = 6;
			this.label1.Text = "Parse Mode:";
			// 
			// comboBoxScope
			// 
			this.comboBoxScope.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.comboBoxScope.FormattingEnabled = true;
			this.comboBoxScope.Location = new System.Drawing.Point(383, 107);
			this.comboBoxScope.Name = "comboBoxScope";
			this.comboBoxScope.Size = new System.Drawing.Size(135, 21);
			this.comboBoxScope.TabIndex = 5;
			this.comboBoxScope.SelectedIndexChanged += new System.EventHandler(this.comboBoxScope_SelectedIndexChanged);
			this.comboBoxScope.TextChanged += new System.EventHandler(this.comboBoxScope_TextChanged);
			// 
			// buttonOptions
			// 
			this.buttonOptions.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.buttonOptions.Location = new System.Drawing.Point(848, 3);
			this.buttonOptions.Name = "buttonOptions";
			this.buttonOptions.Size = new System.Drawing.Size(145, 23);
			this.buttonOptions.TabIndex = 4;
			this.buttonOptions.Text = "Compiler Options";
			this.buttonOptions.UseVisualStyleBackColor = true;
			this.buttonOptions.Click += new System.EventHandler(this.buttonOptions_Click);
			// 
			// buttonCrossCheck
			// 
			this.buttonCrossCheck.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.buttonCrossCheck.Location = new System.Drawing.Point(93, 105);
			this.buttonCrossCheck.Name = "buttonCrossCheck";
			this.buttonCrossCheck.Size = new System.Drawing.Size(127, 23);
			this.buttonCrossCheck.TabIndex = 3;
			this.buttonCrossCheck.Text = "Move Output To Input";
			this.buttonCrossCheck.UseVisualStyleBackColor = true;
			this.buttonCrossCheck.Click += new System.EventHandler(this.buttonCrossCheck_Click);
			// 
			// buttonEval
			// 
			this.buttonEval.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.buttonEval.Location = new System.Drawing.Point(524, 105);
			this.buttonEval.Name = "buttonEval";
			this.buttonEval.Size = new System.Drawing.Size(99, 23);
			this.buttonEval.TabIndex = 2;
			this.buttonEval.Text = "Inline-Evaluate";
			this.buttonEval.UseVisualStyleBackColor = true;
			this.buttonEval.Click += new System.EventHandler(this.buttonEval_Click);
			// 
			// buttonCompile
			// 
			this.buttonCompile.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.buttonCompile.Location = new System.Drawing.Point(12, 105);
			this.buttonCompile.Name = "buttonCompile";
			this.buttonCompile.Size = new System.Drawing.Size(75, 23);
			this.buttonCompile.TabIndex = 1;
			this.buttonCompile.Text = "Compile";
			this.buttonCompile.UseVisualStyleBackColor = true;
			this.buttonCompile.Click += new System.EventHandler(this.buttonCompile_Click);
			// 
			// MainForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(996, 494);
			this.Controls.Add(this.splitContainer1);
			this.KeyPreview = true;
			this.Name = "MainForm";
			this.Text = "GSC Script Test";
			this.Load += new System.EventHandler(this.MainForm_Load);
			this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.MainForm_KeyDown);
			this.splitContainer1.Panel1.ResumeLayout(false);
			this.splitContainer1.Panel2.ResumeLayout(false);
			this.splitContainer1.Panel2.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
			this.splitContainer1.ResumeLayout(false);
			this.splitContainer2.Panel1.ResumeLayout(false);
			this.splitContainer2.Panel2.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).EndInit();
			this.splitContainer2.ResumeLayout(false);
			this.splitContainer3.Panel1.ResumeLayout(false);
			this.splitContainer3.Panel2.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.splitContainer3)).EndInit();
			this.splitContainer3.ResumeLayout(false);
			this.contextMenuStrip1.ResumeLayout(false);
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.SplitContainer splitContainer1;
		private System.Windows.Forms.SplitContainer splitContainer2;
		private System.Windows.Forms.RichTextBox richTextBoxInput;
		private System.Windows.Forms.Button buttonCompile;
		private System.Windows.Forms.Button buttonOptions;
		private System.Windows.Forms.Button buttonCrossCheck;
		private System.Windows.Forms.Button buttonEval;
		private System.Windows.Forms.RichTextBox richTextBoxOutput;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.ComboBox comboBoxScope;
		private System.Windows.Forms.ListView listViewLog;
		private System.Windows.Forms.Button buttonGraph;
		private System.Windows.Forms.Button button1;
		private System.Windows.Forms.Button buttonPack;
		private System.Windows.Forms.SplitContainer splitContainer3;
		private System.Windows.Forms.Button button2;
		private System.Windows.Forms.TreeView treeView1;
		private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
		private System.Windows.Forms.ToolStripMenuItem openToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem createFolderToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem renameToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem createSnippetToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem removeToolStripMenuItem;
	}
}

