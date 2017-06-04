using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Windows.Forms;

namespace GscCodeTest
{
	public class ExtraItem
	{
		public String Text;
		public Func<Boolean> Enabled;
		public Action Click;
	}

	static class TextBoxExt
	{
		public static void InitExt(this TextBoxBase tb, params ExtraItem[] extras)
		{
			tb.PreviewKeyDown += new PreviewKeyDownEventHandler(tb_PreviewKeyDown);
			tb.ContextMenuStrip = CreateStandardContextMenu(extras);
		}

		#region Standard CMS

		static void cutToolStripMenuItem_Click(object sender, EventArgs e)
		{
			var mi = sender as ToolStripMenuItem;
			var cm = (ContextMenuStrip)mi.Owner;
			var sc = (TextBoxBase)cm.SourceControl;
			sc.Cut();
		}

		static void contextMenuStrip1_Opening(object sender, System.ComponentModel.CancelEventArgs e)
		{
			var cm = sender as ContextMenuStrip;
			var sc = cm.SourceControl as TextBoxBase;
			e.Cancel = false;
			cm.Items["selectall"].Enabled = sc.TextLength > 0;
			cm.Items["copy"].Enabled = sc.SelectionLength > 0;
			cm.Items["cut"].Enabled = !sc.ReadOnly && sc.SelectionLength > 0;
			cm.Items["paste"].Enabled = !sc.ReadOnly && Clipboard.ContainsText();

			foreach (var tsi in cm.Tag as IEnumerable<ToolStripMenuItem>)
				if (tsi.Tag != null)
					tsi.Enabled = (tsi.Tag as Func<bool>)();
		}

		static void selectAllToolStripMenuItem_Click(object sender, EventArgs e)
		{
			var mi = sender as ToolStripMenuItem;
			var cm = (ContextMenuStrip)mi.Owner;
			var sc = (TextBoxBase)cm.SourceControl;
			sc.SelectAll();
		}

		static void copyToolStripMenuItem_Click(object sender, EventArgs e)
		{
			var mi = sender as ToolStripMenuItem;
			var cm = (ContextMenuStrip)mi.Owner;
			var sc = (TextBoxBase)cm.SourceControl;
			sc.Copy();
		}

		static void pasteToolStripMenuItem_Click(object sender, EventArgs e)
		{
			var mi = sender as ToolStripMenuItem;
			var cm = (ContextMenuStrip)mi.Owner;
			var sc = (TextBoxBase)cm.SourceControl;
			sc.Paste();
		}
		#endregion


		public static ContextMenuStrip CreateStandardContextMenu(params ExtraItem[] extras)
		{
			ContextMenuStrip cms = new ContextMenuStrip();

			var tsi = new ToolStripMenuItem() { Text = "Select All", Name = "selectall" };
			tsi.Click += new EventHandler(TextBoxExt.selectAllToolStripMenuItem_Click);
			cms.Items.Add(tsi);
			cms.Items.Add(new ToolStripSeparator());

			tsi = new ToolStripMenuItem() { Text = "Cut", Name = "cut" };
			tsi.Click += new EventHandler(TextBoxExt.cutToolStripMenuItem_Click);
			cms.Items.Add(tsi);

			tsi = new ToolStripMenuItem() { Text = "Copy", Name = "copy" };
			tsi.Click += new EventHandler(TextBoxExt.copyToolStripMenuItem_Click);
			cms.Items.Add(tsi);

			tsi = new ToolStripMenuItem { Text = "Paste", Name = "paste" };
			tsi.Click += new EventHandler(TextBoxExt.pasteToolStripMenuItem_Click);
			cms.Items.Add(tsi);

			var extraitems = new List<ToolStripMenuItem>();
			if (extras != null)
			{
				cms.Items.Add(new ToolStripSeparator());
				foreach (var e in extras)
				{
					var i = new ToolStripMenuItem() { Text = e.Text };
					i.Tag = e.Enabled;
					if (e.Click != null)
						i.Click += new EventHandler((s, ev) => { e.Click(); });
					cms.Items.Add(i);
					extraitems.Add(i);
				}
			}

			cms.Tag = extraitems;

			cms.Name = "StandardContextMenuStrip";
			//this.contextMenuStrip1.Size = new System.Drawing.Size(123, 98);
			cms.Opening += new System.ComponentModel.CancelEventHandler(TextBoxExt.contextMenuStrip1_Opening);

			return cms;
		}

		static void tb_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
		{
			if (e.KeyCode == Keys.Tab)
				e.IsInputKey = true;
		}
	}

	
}
