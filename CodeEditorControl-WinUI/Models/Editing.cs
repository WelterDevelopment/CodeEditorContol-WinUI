using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeEditorControl_WinUI;
public class EditAction
{
	public EditActionType EditActionType { get; set; }
	public string TextState { get; set; }
	public string TextInvolved { get; set; }
	public Range Selection { get; set; }
}
