using Microsoft.VisualStudio.Shell;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace For_the_Darkest_Dungeon
{
	/// <summary>
	/// 全局功能的可配置选项。
	/// </summary>
	[ClassInterface(ClassInterfaceType.AutoDual)]
	[Guid("A4C67610-1A2E-4CF1-9F42-2C2A5A597A5D")]
	public class GeneralOptionsPage : DialogPage
	{
		[Category("General")]
		[DisplayName("启用 Ctrl+/ 快捷注释")]
		[Description("控制是否对所有 *.darkest 文件启用 Ctrl+/ 多行快捷注释与取消注释功能。")]
		public bool EnableCtrlSlashToggleComment { get; set; } = true;
	}
}