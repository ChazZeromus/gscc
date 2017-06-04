namespace GSC_easyui
{
	partial class WorkSet
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

		#region Component Designer generated code

		/// <summary> 
		/// Required method for Designer support - do not modify 
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.FileQueue = new System.Windows.Forms.ListView();
			this.columnHeaderFilename = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeaderLocation = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeaderStatus = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.SuspendLayout();
			// 
			// FileQueue
			// 
			this.FileQueue.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
			this.FileQueue.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeaderFilename,
            this.columnHeaderLocation,
            this.columnHeaderStatus});
			this.FileQueue.Location = new System.Drawing.Point(3, 3);
			this.FileQueue.Name = "FileQueue";
			this.FileQueue.Size = new System.Drawing.Size(237, 293);
			this.FileQueue.TabIndex = 1;
			this.FileQueue.UseCompatibleStateImageBehavior = false;
			this.FileQueue.View = System.Windows.Forms.View.Details;
			// 
			// columnHeaderFilename
			// 
			this.columnHeaderFilename.Text = "File";
			this.columnHeaderFilename.Width = 71;
			// 
			// columnHeaderLocation
			// 
			this.columnHeaderLocation.Text = "Location";
			this.columnHeaderLocation.Width = 82;
			// 
			// columnHeaderStatus
			// 
			this.columnHeaderStatus.Text = "Status";
			this.columnHeaderStatus.Width = 76;
			// 
			// WorkSet
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.Controls.Add(this.FileQueue);
			this.Name = "WorkSet";
			this.Size = new System.Drawing.Size(444, 299);
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.ListView FileQueue;
		private System.Windows.Forms.ColumnHeader columnHeaderFilename;
		private System.Windows.Forms.ColumnHeader columnHeaderLocation;
		private System.Windows.Forms.ColumnHeader columnHeaderStatus;
	}
}
