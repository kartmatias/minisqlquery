﻿using System;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Windows.Forms;
using ICSharpCode.TextEditor.Document;
using MiniSqlQuery.Commands;
using MiniSqlQuery.Core;
using WeifenLuo.WinFormsUI.Docking;

namespace MiniSqlQuery
{
	public partial class QueryForm : DockContent, IQueryEditor, IPrintableContent
	{
		private static int _untitledCounter = 1;
		private readonly IApplicationServices _services;

		private bool _highlightingProviderLoaded;
		private bool _isDirty;
		private QueryRunner _runner;

		private string _status = string.Empty;
		private ITextFindService _textFindService;

		public QueryForm()
		{
			InitializeComponent();

			txtQuery.ContextMenuStrip = contextMenuStripQuery;
			LoadHighlightingProvider();
			txtQuery.Document.DocumentChanged += DocumentDocumentChanged;

			queryToolStripMenuItem.DropDownItems.Add(CommandControlBuilder.CreateToolStripMenuItem<ExecuteQueryCommand>());
			queryToolStripMenuItem.DropDownItems.Add(CommandControlBuilder.CreateToolStripMenuItem<SaveResultsAsDataSetCommand>());
			queryToolStripMenuItem.DropDownItems.Add(CommandControlBuilder.CreateToolStripMenuItemSeperator());

			contextMenuStripQuery.Items.Add(CommandControlBuilder.CreateToolStripMenuItem<ExecuteQueryCommand>());

			editorContextMenuStrip.Items.Add(CommandControlBuilder.CreateToolStripMenuItem<SaveFileCommand>());
			editorContextMenuStrip.Items.Add(CommandControlBuilder.CreateToolStripMenuItemSeperator());
			editorContextMenuStrip.Items.Add(CommandControlBuilder.CreateToolStripMenuItem<CloseActiveWindowCommand>());
			editorContextMenuStrip.Items.Add(CommandControlBuilder.CreateToolStripMenuItem<CloseAllWindowsCommand>());
			editorContextMenuStrip.Items.Add(CommandControlBuilder.CreateToolStripMenuItem<CopyQueryEditorFileNameCommand>());

			CommandControlBuilder.MonitorMenuItemsOpeningForEnabling(editorContextMenuStrip);
		}

		public QueryForm(IApplicationServices services)
			: this()
		{
			_services = services;
		}

		#region IPrintableContent Members

		public PrintDocument PrintDocument
		{
			get { return txtQuery.PrintDocument; }
		}

		#endregion

		#region IQueryEditor Members

		public string SelectedText
		{
			get { return txtQuery.ActiveTextAreaControl.SelectionManager.SelectedText; }
		}

		public string AllText
		{
			get { return txtQuery.Text; }
			set { txtQuery.Text = value; }
		}

		public Control EditorControl
		{
			get { return txtQuery; }
		}


		public string FileName
		{
			get { return txtQuery.FileName; }
			set
			{
				txtQuery.FileName = value;

				SetTabTextByFilename();
			}
		}

		public bool IsDirty
		{
			get { return _isDirty; }
			set
			{
				if (_isDirty != value)
				{
					_isDirty = value;
					SetTabTextByFilename();
				}
			}
		}

		public bool IsBusy { get; private set; }

		[Obsolete]
		public DataSet DataSet { get; private set; }

		public void SetStatus(string text)
		{
			_status = text;
			UpdateHostStatus();
		}

		public void SetSyntax(string name)
		{
			LoadHighlightingProvider();
			txtQuery.SetHighlighting(name);
		}

		public void LoadFile()
		{
			txtQuery.LoadFile(FileName);
			IsDirty = false;
		}

		public void SaveFile()
		{
			if (FileName == null)
			{
				throw new InvalidOperationException("The 'FileName' cannot be null");
			}

			txtQuery.SaveFile(FileName);
			IsDirty = false;
		}


		public void ExecuteQuery()
		{
			if (!string.IsNullOrEmpty(SelectedText))
			{
				ExecuteQuery(SelectedText);
			}
			else
			{
				ExecuteQuery(AllText);
			}
		}

		public void InsertText(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return;
			}

			int offset = txtQuery.ActiveTextAreaControl.Caret.Offset;

			// if some text is selected we want to replace it
			if (txtQuery.ActiveTextAreaControl.SelectionManager.IsSelected(offset))
			{
				offset = txtQuery.ActiveTextAreaControl.SelectionManager.SelectionCollection[0].Offset;
				txtQuery.ActiveTextAreaControl.SelectionManager.RemoveSelectedText();
			}

			txtQuery.Document.Insert(offset, text);
			int newOffset = offset + text.Length; // new offset at end of inserted text

			// now reposition the caret if required to be after the inserted text
			if (CursorOffset != newOffset)
			{
				SetCursorByOffset(newOffset);
			}

			txtQuery.Focus();
		}


		public void ClearSelection()
		{
			txtQuery.ActiveTextAreaControl.SelectionManager.ClearSelection();
		}

		public bool SetCursorByOffset(int offset)
		{
			if (offset >= 0)
			{
				txtQuery.ActiveTextAreaControl.Caret.Position = txtQuery.Document.OffsetToPosition(offset);
				return true;
			}

			return false;
		}

		public bool SetCursorByLocation(int line, int column)
		{
			if (line > TotalLines)
			{
				return false;
			}

			txtQuery.ActiveTextAreaControl.Caret.Line = line;
			txtQuery.ActiveTextAreaControl.Caret.Column = column;

			return true;
		}

		public int CursorLine
		{
			get { return txtQuery.ActiveTextAreaControl.Caret.Line; }
			set { txtQuery.ActiveTextAreaControl.Caret.Line = value; }
		}

		public int CursorColumn
		{
			get { return txtQuery.ActiveTextAreaControl.Caret.Column; }
			set { txtQuery.ActiveTextAreaControl.Caret.Column = value; }
		}

		public void HighlightString(int offset, int length)
		{
			if (offset < 0 || length < 1)
			{
				return;
			}

			int endPos = offset + length;
			txtQuery.ActiveTextAreaControl.SelectionManager.SetSelection(
				txtQuery.Document.OffsetToPosition(offset),
				txtQuery.Document.OffsetToPosition(endPos));
			SetCursorByOffset(endPos);
		}

		public int TotalLines
		{
			get { return txtQuery.Document.TotalNumberOfLines; }
		}

		public int CursorOffset
		{
			get { return txtQuery.ActiveTextAreaControl.Caret.Offset; }
		}

		public ITextFindService TextFindService
		{
			get
			{
				if (_textFindService == null)
				{
					_textFindService = _services.Container.Resolve<ITextFindService>("DefaultTextFindService");
				}
				return _textFindService;
			}
		}

		public void SetTextFindService(ITextFindService textFindService)
		{
			// accept nulls infering a reset
			_textFindService = textFindService;
		}

		public bool CanReplaceText
		{
			get { return true; }
		}

		public int FindString(string value, int startIndex, StringComparison comparisonType)
		{
			if (string.IsNullOrEmpty(value) || startIndex < 0)
			{
				return -1;
			}

			string text = AllText;
			int pos = text.IndexOf(value, startIndex, comparisonType);
			if (pos > -1)
			{
				ClearSelection();
				HighlightString(pos, value.Length);
			}

			return pos;
		}

		public bool ReplaceString(string value, int startIndex, int length)
		{
			if (value == null)
			{
				return false;
			}

			if ((startIndex + length) > AllText.Length)
			{
				return false;
			}

			txtQuery.Document.Replace(startIndex, length, value);

			return true;
		}

		/// <summary>
		/// Gets a reference to the batch of queries.
		/// </summary>
		/// <value>The query batch.</value>
		public QueryBatch Batch
		{
			get { return _runner == null ? null : _runner.Batch; }
		}

		#endregion

		public void LoadHighlightingProvider()
		{
			if (_highlightingProviderLoaded)
			{
				return;
			}

			// see: http://wiki.sharpdevelop.net/Syntax%20highlighting.ashx
			string dir = Path.GetDirectoryName(GetType().Assembly.Location);
			FileSyntaxModeProvider fsmProvider = new FileSyntaxModeProvider(dir);
			HighlightingManager.Manager.AddSyntaxModeFileProvider(fsmProvider); // Attach to the text editor.
			txtQuery.SetHighlighting("SQL");
			_highlightingProviderLoaded = true;
		}

		private void SetTabTextByFilename()
		{
			string dirty = string.Empty;
			string text = "Untitled";

			if (_isDirty)
			{
				dirty = " *";
			}

			if (txtQuery.FileName != null)
			{
				text = FileName;
			}
			else
			{
				text += _untitledCounter;
				_untitledCounter++;
			}

			text += dirty;
			TabText = text;
			ToolTipText = text;
		}

		private void DocumentDocumentChanged(object sender, DocumentEventArgs e)
		{
			IsDirty = true;
		}

		protected void UpdateHostStatus()
		{
			ApplicationServices.Instance.HostWindow.SetStatus(this, _status);
		}

		public void ExecuteQuery(string sql)
		{
			IApplicationSettings settings = _services.Settings;

			if (IsBusy)
			{
				_services.HostWindow.DisplaySimpleMessageBox(this, "Please wait for the current operation to complete.", "Busy");
				return;
			}

			_runner = QueryRunner.Create(settings.ProviderFactory, settings.ConnectionDefinition.ConnectionString, settings.EnableQueryBatching);
			queryBackgroundWorker.RunWorkerAsync(sql);
		}

		private static string CreateQueryCompleteMessage(DateTime start, DateTime end)
		{
			TimeSpan ts = end.Subtract(start);
			string msg = string.Format(
				"Query complete, {0:00}:{1:00}.{2:000}",
				ts.Minutes,
				ts.Seconds,
				ts.Milliseconds);
			return msg;
		}

		private void AddTables()
		{
			_resultsTabControl.TabPages.Clear();

			if (Batch != null)
			{
				int counter = 1;
				foreach (Query query in Batch.Queries)
				{
					DataSet ds = query.Result;
					if (query.Result != null)
					{
						foreach (DataTable dt in ds.Tables)
						{
							DataGridView grid = new DataGridView();
							DataGridViewCellStyle cellStyle = new DataGridViewCellStyle();

							grid.AllowUserToAddRows = false;
							grid.AllowUserToDeleteRows = false;
							grid.Dock = DockStyle.Fill;
							grid.Name = "gridResults_" + counter;
							grid.ReadOnly = true;
							grid.DataSource = dt;
							grid.DataError += GridDataError;
							grid.DefaultCellStyle = cellStyle;
							grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
							grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;

							cellStyle.NullValue = "<NULL>";
							cellStyle.Font = CreateDefaultFont();

							TabPage tabPage = new TabPage();
							tabPage.Controls.Add(grid);
							tabPage.Name = "tabPageResults_" + counter;
							tabPage.Padding = new Padding(3);
							tabPage.Text = string.Format("{0}/Table {1}", ds.DataSetName, counter);
							tabPage.UseVisualStyleBackColor = false;

							_resultsTabControl.TabPages.Add(tabPage);
							counter++;
						}
					}
				}

				if (!string.IsNullOrEmpty(Batch.Messages))
				{
					RichTextBox rtf = new RichTextBox();
					rtf.Font = CreateDefaultFont();
					rtf.Dock = DockStyle.Fill;
					rtf.ScrollBars = RichTextBoxScrollBars.ForcedBoth;
					rtf.Text = Batch.Messages;

					TabPage tabPage = new TabPage();
					tabPage.Controls.Add(rtf);
					tabPage.Name = "tabPageResults_Messages";
					tabPage.Padding = new Padding(3);
					tabPage.Dock = DockStyle.Fill;
					tabPage.Text = "Messages";
					tabPage.UseVisualStyleBackColor = false;

					_resultsTabControl.TabPages.Add(tabPage);
				}
			}
		}

		protected Font CreateDefaultFont()
		{
			return new Font("Courier New", 8.25F, FontStyle.Regular, GraphicsUnit.Point);
		}

		private void GridDataError(object sender, DataGridViewDataErrorEventArgs e)
		{
			e.ThrowException = false;
		}

		private void QueryForm_Load(object sender, EventArgs e)
		{
		}

		private void QueryForm_Activated(object sender, EventArgs e)
		{
			UpdateHostStatus();
		}

		private void QueryForm_Deactivate(object sender, EventArgs e)
		{
			_services.HostWindow.SetStatus(this, string.Empty);
		}

		private void QueryForm_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (_isDirty)
			{
				DialogResult saveFile = _services.HostWindow.DisplayMessageBox(
					this,
					"Contents changed, do you want to save the file?\r\n" + TabText, "Save Changes?",
					MessageBoxButtons.YesNoCancel,
					MessageBoxIcon.Question,
					MessageBoxDefaultButton.Button1,
					0,
					null,
					null);

				if (saveFile == DialogResult.Cancel)
				{
					e.Cancel = true;
				}
				else if (saveFile == DialogResult.Yes)
				{
					CommandManager.GetCommandInstance<SaveFileCommand>().Execute();
				}
			}
		}

		private void queryBackgroundWorker_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
		{
			string sql = (string) e.Argument;
			_runner.BatchProgress += RunnerBatchProgress;
			_runner.ExecuteQuery(sql);
		}

		private void RunnerBatchProgress(object sender, BatchProgressEventArgs e)
		{
			// push the progress through to the background worker
			queryBackgroundWorker.ReportProgress(e.Index);
		}

		private void queryBackgroundWorker_ProgressChanged(object sender, System.ComponentModel.ProgressChangedEventArgs e)
		{
			SetStatus(string.Format("Query# {0}.", e.ProgressPercentage));
		}

		private void queryBackgroundWorker_RunWorkerCompleted(object sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
		{
			_runner.BatchProgress -= RunnerBatchProgress;
			if (e.Error != null)
			{
				// todo: improve!
				_services.HostWindow.DisplaySimpleMessageBox(this, e.Error.Message, "Error");
				SetStatus(e.Error.Message);
			}
			else
			{
				try
				{
					_services.HostWindow.SetPointerState(Cursors.Default);
					string message = CreateQueryCompleteMessage(_runner.Batch.StartTime, _runner.Batch.EndTime);
					if (_runner.Exception != null)
					{
						message = "ERROR - " + message;
					}
					_services.HostWindow.SetStatus(this, message);
					AddTables();
					txtQuery.Focus();
				}
				finally
				{
					txtQuery.Enabled = true;
					IsBusy = false;
				}
			}
		}
	}
}