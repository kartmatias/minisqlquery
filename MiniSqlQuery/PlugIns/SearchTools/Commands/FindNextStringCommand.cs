﻿using System;
using System.Windows.Forms;
using MiniSqlQuery.Core;
using MiniSqlQuery.Core.Commands;

namespace MiniSqlQuery.PlugIns.SearchTools.Commands
{
	public class FindNextStringCommand : CommandBase
	{
		public FindNextStringCommand() : base("Find Next String")
		{
			SmallImage = ImageResource.find;
			ShortcutKeys = Keys.F3;
		}

		/// <summary>
		/// Two execution methods - through the "Find Text" window or by hitting F3.
		/// If simply hitting F3, we are executing the current search from current cursor position for this window.
		/// Different windows can also have different search text.
		/// </summary>
		public override void Execute()
		{
			IFindReplaceProvider editorFindProvider = Services.HostWindow.ActiveChildForm as IFindReplaceProvider;

			if (editorFindProvider != null)
			{
				FindTextRequest findTextRequest = null;
				int key = editorFindProvider.GetHashCode();

				// is there a request in the table for this window?
				if (SearchToolsCommon.FindReplaceTextRequests.ContainsKey(key))
				{
					findTextRequest = SearchToolsCommon.FindReplaceTextRequests[key];
				}
				else
				{
					// none in table, default to curently selected text if its the editor
					IQueryEditor editor = Services.HostWindow.ActiveChildForm as IQueryEditor;

					if (editor != null && editor.SelectedText.Length > 0)
					{
						findTextRequest = new FindTextRequest(editorFindProvider, editor.SelectedText);
					}
				}

				if (findTextRequest != null)
				{
					// wrap around to start if at last pos
					if (findTextRequest.Position != 0)
					{
						findTextRequest.Position = editorFindProvider.CursorOffset;
					}

					findTextRequest = editorFindProvider.TextFindService.FindNext(findTextRequest);
					SearchToolsCommon.FindReplaceTextRequests[key] = findTextRequest;
				}
			}
		}
	}
}