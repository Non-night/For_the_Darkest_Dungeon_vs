using Microsoft.VisualStudio.Shell;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace For_the_Darkest_Dungeon
{
	/// <summary>
	/// Colours 功能的可配置选项。
	/// </summary>
	[ClassInterface(ClassInterfaceType.AutoDual)]
	[Guid("0F4A7D61-8CE4-4D69-8B5A-5C2C8D4B8701")]
	public class ColoursOptionsPage : DialogPage
	{
		[Category("Colours")]
		[DisplayName("自动渲染颜色预览")]
		[Description("控制是否在 .rgba 参数后自动显示颜色预览方块。")]
		public bool EnableAutomaticColorPreview { get; set; } = true;

		[Category("Colours")]
		[DisplayName("点击颜色方块打开调色盘")]
		[Description("控制点击颜色预览方块时是否弹出调色盘窗口。")]
		public bool EnableColorPickerOnClick { get; set; } = true;
	}
}