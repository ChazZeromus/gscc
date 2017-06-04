using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using GameScriptCompiler;
using GameScriptCompiler.CodeDOM;
using GameScriptCompiler.Text;
using System.IO;

namespace GscCodeTest
{
	public partial class MainForm : Form
	{
		public OptionEdit PropertyEditWindow, EvalWindow;
		public GraphView GraphWindow;
		public static String SnippetPath = "Snippets";
		public InlineEvaluator.EvaluationOptions EvalOptions;
		public enum ParseMode
		{
			Module,
			Expression
		}

		public static String SettingsFile = "Settings.xml";
		CompilerFrontEnd Cfe;
		Expression FinalExpr = null;
		StatementBlock FinalBlock = null;
		Expressable FinalExpressable = null;

		public MainForm()
		{
			InitializeComponent();
			EvalOptions = new InlineEvaluator.EvaluationOptions();
			foreach (var item in Enum.GetNames(typeof(ScopeMode)))
				comboBoxScope.Items.Add(item);
			comboBoxScope.SelectedIndex = 0;
			richTextBoxInput.InitExt(new ExtraItem[] { new ExtraItem() { Text = "Show Snippets", Enabled = () =>
			{
				return this.splitContainer3.Panel1Collapsed;
			}, Click = () => { this.splitContainer3.Panel1Collapsed = false; } }});
			richTextBoxOutput.InitExt();

			richTextBoxOutput.Font = richTextBoxInput.Font;
		}
		
		private void MainForm_Load(object sender, EventArgs e)
		{
			Cfe = new CompilerFrontEnd();
			Cfe.Options = new CompilerOptions();
			try
			{
				if (File.Exists(SettingsFile))
					using (var s = File.OpenRead(SettingsFile))
						Cfe.Options.LoadOptions(s);
				else
					using (var s = File.OpenWrite(SettingsFile))
						Cfe.Options.SaveOptions(s);
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.ToString());
			}

			PropertyEditWindow = new OptionEdit(Cfe.Options);
			GraphWindow = new GraphView(this);
			EvalWindow = new OptionEdit(EvalOptions);

			if (!Directory.Exists(SnippetPath))
				Directory.CreateDirectory(SnippetPath);

			SnipFillDir(null);

			richTextBoxInput.Focus();
			richTextBoxInput.Select();
		}

		void ClearLog()
		{
			listViewLog.Clear();
		}

		void Log(String text)
		{
			listViewLog.Items.Add(new ListViewItem(text));
		}

		void Log(String text, Color color)
		{
			listViewLog.Items.Add(new ListViewItem(text) { BackColor = color });
		}

		void Log(String text, Color color, Object tag)
		{
			listViewLog.Items.Add(new ListViewItem(text) { BackColor = color, Tag = tag });
		}

		void Pack()
		{
			ClearLog();
			richTextBoxOutput.Clear();
			FinalExpr = null;
			FinalBlock = null;
			var enc = new UTF8Encoding(false);
			using (var sr = new StreamReader(new MemoryStream(enc.GetBytes(richTextBoxInput.Text))))
			using (var sb = new SyntaxBuilder(Cfe, sr, SelectedScope))
			{
				while (sb.Step())
					if (sb.Stage == SyntaxBuilder.Stages.Syntaxing)
						break;
				foreach (var msg in sb.Warns)
					Log(msg.Msg, Color.DarkOrange, msg.Location);
				foreach (var msg in sb.Errors)
					Log(msg.Msg, Color.LightPink, msg.Location);

				if (sb.Stage != SyntaxBuilder.Stages.Syntaxing)
				{
					Log("Tokenizing failed!", Color.Pink);
					return;
				}
				else
				{
					using (var ms = new MemoryStream())
					{
						using (var sw = new StreamWriter(ms))
							Packer.PackToMinimumWhitespace(sb.Tokens.ToArray(), sw);
						richTextBoxOutput.Text = enc.GetString(ms.ToArray());
					}
					Log("Packing Completed!", Color.LightSeaGreen);
				}
			}
		}

		void Eval()
		{
			if (SelectedScope != ScopeMode.Expression)
				return;
			Compile();
			Eval(FinalExpr);
		}

		public void Eval(Expression expr)
		{
			InlineEvaluator ie = new InlineEvaluator(Cfe.Options, EvalOptions);
			if (expr == null)
			{
				Log("Expression in output is not resolvable!", Color.Orange);
				return;
			}
			ie.Evaluate(expr);
			StringBuilder sb = new StringBuilder("Result: " + ie.Result.ResultType.ToString());
			sb.AppendLine();
			if (ie.Result.ResultType == InlineEvaluator.EvaluationResult.Success)
			{
				sb.AppendLine("Evaluated: {0}".Fmt(ie.Result.Result.Express(0)));
				sb.AppendLine("Evaluated Type: {0}".Fmt(ie.Result.Result.GetIEDataType().ToString()));
			}
			else
				sb.AppendLine("Failed At: {0}".Fmt(ie.Result.ErrorExpr.Express(0)));
			richTextBoxOutput.Text = sb.ToString();
		}

		void Compile()
		{
			ClearLog();
			richTextBoxOutput.Clear();
			FinalExpr = null;
			FinalBlock = null;
			FinalExpressable = null;
			var enc = new UTF8Encoding(false);
			using (var sr = new StreamReader(new MemoryStream(enc.GetBytes(richTextBoxInput.Text))))
			using (var sb = new SyntaxBuilder(Cfe, sr, SelectedScope))
			{
				while (sb.Step()) ;

				foreach (var msg in sb.Warns)
					Log(msg.Msg, Color.DarkOrange, msg.Location);
				foreach (var msg in sb.Errors)
					Log(msg.Msg, Color.LightPink, msg.Location);

				if (!sb.Success)
				{
					Log("Compiling failed!", Color.Pink);
					return;
				}
				else
				{
					Log("Compile Successful!", Color.LightSeaGreen);
					if (SelectedScope == ScopeMode.Expression)
						FinalExpr = sb.Syn.CreateFinalExpression();
					else
						if (SelectedScope == ScopeMode.Function)
							FinalBlock = sb.Syn.CreateFinalFunction().RootStatement;
					FinalExpressable = sb.GetExpressable();
					richTextBoxOutput.Text = FinalExpressable.Express(0);
				}
			}
		}

		void Replace()
		{
			richTextBoxInput.Clear();
			richTextBoxInput.Text = richTextBoxOutput.Text;
			richTextBoxOutput.Clear();
		}

		private void buttonCompile_Click(object sender, EventArgs e)
		{
			Compile();
		}


		ScopeMode SelectedScope
		{
			get
			{
				ScopeMode scope = ScopeMode.Module;
				Enum.TryParse(comboBoxScope.SelectedItem as String, out scope);
				return scope;
			}
		}

		private void comboBoxScope_TextChanged(object sender, EventArgs e)
		{
			comboBoxScope.SelectedIndex = comboBoxScope.SelectedIndex;
		}

		private void buttonOptions_Click(object sender, EventArgs e)
		{
			if (PropertyEditWindow.Visible)
				PropertyEditWindow.Hide();
			else
			{
				PropertyEditWindow.Show();
				PropertyEditWindow.Focus();
			}
		}

		private void listViewLog_DoubleClick(object sender, EventArgs e)
		{
			if (listViewLog.SelectedItems.Count == 0
				|| listViewLog.SelectedItems[0].Tag == null)
				return;
			var loc = listViewLog.SelectedItems[0].Tag as DocLocation?;
			GoToLoc(loc);
		}

		public void GoToLoc(DocLocation? location)
		{
			richTextBoxInput.Focus();
			richTextBoxInput.Select();
			richTextBoxInput.Select((int)location.Value.Offset, 0);
			richTextBoxInput.ScrollToCaret();
		}

		private void MainForm_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Shift && e.Control)
			{
				e.Handled = true;
				switch (e.KeyCode)
				{
					case Keys.C:
						this.Invoke(new Action(this.Compile));
						break;
					case Keys.R:
						this.Invoke(new Action(this.Replace));
						break;
					case Keys.E:
						this.Invoke(new Action(this.Eval));
						break;
					default:
						e.Handled = false;
						break;
				}
			}
		}

		private void buttonEval_Click(object sender, EventArgs e)
		{
			Eval();
		}

		private void comboBoxScope_SelectedIndexChanged(object sender, EventArgs e)
		{
			buttonEval.Enabled = SelectedScope == ScopeMode.Expression;
		}

		private void buttonCrossCheck_Click(object sender, EventArgs e)
		{
			Replace();
		}

		Expression TargetFinal
		{
			get
			{
				return SelectedScope == ScopeMode.Function ? FinalBlock : FinalExpr;
			}
		}

		private void buttonGraph_Click(object sender, EventArgs e)
		{
			Compile();
			if (FinalExpressable != null)
			{
				GraphWindow.Show();
				GraphWindow.Focus();
				GraphWindow.SetUpNodes(FinalExpressable);
			}
			else
				Log("Cannot view graph.", Color.Orange);
		}

		private void button1_Click(object sender, EventArgs e)
		{
			EvalWindow.Show();
			EvalWindow.Focus();
		}

		private void buttonPack_Click(object sender, EventArgs e)
		{
			Pack();
		}

		private void button2_Click(object sender, EventArgs e)
		{
			splitContainer3.Panel1Collapsed = true;
		}

		private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
		{

		}

		public class SnipNode
		{
			public String Path;
			public Boolean IsFolder;
		}

		private void SnipFillDir(TreeNode root, bool expand = false)
		{
			String targetPath;
			TreeNodeCollection children;
			if (root == null)
			{
				targetPath = MainForm.SnippetPath;
				children = treeView1.Nodes;
			}
			else
			{
				targetPath = (root.Tag as SnipNode).Path;
				children = root.Nodes;
			}

			children.Clear();

			DirectoryInfo di = new DirectoryInfo(targetPath);
			if (!di.Exists)
				return;
			foreach (var d in di.GetDirectories())
			{
				TreeNode n = new TreeNode() { Text = "[Folder]{0}".Fmt(d.Name), Tag = new SnipNode() { IsFolder = true, Path = d.FullName } };
				children.Add(n);
				SnipFillDir(n);
			}

			foreach (var f in di.GetFiles())
			{
				TreeNode n = new TreeNode() { Text = f.Name, Tag = new SnipNode() { IsFolder = false, Path = f.FullName }};
				children.Add(n);
			}

			if (expand)
				if (root != null)
					root.Expand();
		}

		private void contextMenuStrip1_Opening(object sender, CancelEventArgs e)
		{
			if (treeView1.SelectedNode != null)
			{
				SnipNode sn = treeView1.SelectedNode.Tag as SnipNode;
				openToolStripMenuItem.Enabled = !sn.IsFolder;
				renameToolStripMenuItem.Enabled = true;
				createFolderToolStripMenuItem.Enabled = true;
				removeToolStripMenuItem.Enabled = true;
			}
			else
			{
				removeToolStripMenuItem.Enabled = false;
				openToolStripMenuItem.Enabled = false;
				renameToolStripMenuItem.Enabled = false;
				createFolderToolStripMenuItem.Enabled = false;
			}
		}

		private void openToolStripMenuItem_Click(object sender, EventArgs e)
		{
			using (StreamReader sr = new StreamReader((treeView1.SelectedNode.Tag as SnipNode).Path))
				richTextBoxInput.Text = sr.ReadToEnd();
		}

		private void createFolderToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SnipNode sn = new SnipNode { Path = null, IsFolder = true };
			TreeNode tn = new TreeNode { Tag = sn };

			if (treeView1.SelectedNode != null)
				if ((treeView1.SelectedNode.Tag as SnipNode).IsFolder)
					treeView1.SelectedNode.Nodes.Add(tn);
				else
					if (treeView1.SelectedNode.Parent != null)
					{
						treeView1.SelectedNode.Parent.Nodes.Add(tn);
					}
					else
						treeView1.Nodes.Add(tn);
			else
				treeView1.Nodes.Add(tn);

			if (tn.Parent != null)
				this.Invoke(new Action(tn.Parent.Expand));
			this.Invoke(new Action(tn.BeginEdit));
		}

		private void treeView1_AfterLabelEdit(object sender, NodeLabelEditEventArgs e)
		{
			e.CancelEdit = true;
			if (e.Label == null)
				return;
			SnipNode sn = e.Node.Tag as SnipNode;
			if (sn.IsFolder)
			{
				try
				{
					if (sn.Path != null)
					{
						DirectoryInfo di = new DirectoryInfo(sn.Path);
						di.MoveTo(Path.Combine(di.Parent.FullName, e.Label));
					}
					else
					{
						String path = Path.Combine(e.Node.Parent == null ? MainForm.SnippetPath : (e.Node.Parent.Tag as SnipNode).Path, e.Label);
						Directory.CreateDirectory(path);
						sn.Path = path;
					}
				}
				catch (Exception ex)
				{
					MessageBox.Show(ex.ToString());
					e.CancelEdit = true;
					e.Node.Remove();
					return;
				}
				e.Node.Text = "[Folder]{0}".Fmt(e.Label);
			}
			else
			{
				try
				{
					if (sn.Path != null)
					{
						FileInfo fi = new FileInfo(sn.Path);
						fi.MoveTo(Path.Combine(fi.DirectoryName, e.Label));
					}
					else
					{
						String path = Path.Combine(e.Node.Parent == null ? MainForm.SnippetPath : (e.Node.Parent.Tag as SnipNode).Path, e.Label);
						using (StreamWriter sw = new StreamWriter(path))
							sw.Write(richTextBoxInput.Text);
						sn.Path = path;
					}
				}
				catch (Exception ex)
				{
					MessageBox.Show(ex.ToString());
					e.CancelEdit = true;
					e.Node.Remove();
					return;
				}
				e.Node.Text = e.Label;
			}
		}

		private void renameToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (treeView1.SelectedNode == null)
				return;
			treeView1.SelectedNode.BeginEdit();
		}

		private void removeToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (treeView1.SelectedNode == null)
				return;
			SnipNode sn = treeView1.SelectedNode.Tag as SnipNode;
			treeView1.SelectedNode.Remove();
			try
			{
				if (sn.IsFolder)
					Directory.Delete(sn.Path, true);
				else
					File.Delete(sn.Path);
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.ToString());
			}
		}

		private void createSnippetToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SnipNode sn = new SnipNode { Path = null, IsFolder = false };
			TreeNode tn = new TreeNode { Tag = sn };
			if (treeView1.SelectedNode != null)
				if ((treeView1.SelectedNode.Tag as SnipNode).IsFolder)
					treeView1.SelectedNode.Nodes.Add(tn);
				else
					if (treeView1.SelectedNode.Parent != null)
						treeView1.SelectedNode.Parent.Nodes.Add(tn);
					else
						treeView1.Nodes.Add(tn);
			else
				treeView1.Nodes.Add(tn);
			tn.BeginEdit();
		}

		private void treeView1_MouseClick(object sender, MouseEventArgs e)
		{
			var ht = treeView1.HitTest(e.Location);
			if (ht.Node != null)
				treeView1.SelectedNode = ht.Node;
		}
	}
}
