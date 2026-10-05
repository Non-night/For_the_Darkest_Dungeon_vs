using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;

namespace For_the_Darkest_Dungeon
{
	/// <summary>
	/// 字符布尔值的大小写偏好。
	/// </summary>
	[TypeConverter(typeof(BooleanTextCasePreferenceConverter))]
	public enum BooleanTextCasePreference
	{
		Lowercase,
		Uppercase,
		TitleCase
	}

	/// <summary>
	/// 支持两种布尔写法时的补全偏好。
	/// </summary>
	[TypeConverter(typeof(BooleanValueStylePreferenceConverter))]
	public enum BooleanValueStylePreference
	{
		Characters,
		Numeric01
	}

	/// <summary>
	/// 将布尔大小写偏好显示为稳定、易懂的中文选项。
	/// </summary>
	internal sealed class BooleanTextCasePreferenceConverter : EnumConverter
	{
		public BooleanTextCasePreferenceConverter() : base(typeof(BooleanTextCasePreference)) { }

		public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
		{
			if (destinationType == typeof(string))
			{
				switch ((BooleanTextCasePreference)value)
				{
					case BooleanTextCasePreference.Uppercase:
						return "全大写";
					case BooleanTextCasePreference.TitleCase:
						return "仅首字母大写";
					default:
						return "全小写";
				}
			}

			return base.ConvertTo(context, culture, value, destinationType);
		}

		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			string text = value as string;
			if (text == "全大写" || text == "Uppercase")
				return BooleanTextCasePreference.Uppercase;
			if (text == "仅首字母大写" || text == "TitleCase")
				return BooleanTextCasePreference.TitleCase;
			if (text == "全小写" || text == "Lowercase")
				return BooleanTextCasePreference.Lowercase;

			return base.ConvertFrom(context, culture, value);
		}
	}

	/// <summary>
	/// 将布尔表示形式偏好显示为中文选项。
	/// </summary>
	internal sealed class BooleanValueStylePreferenceConverter : EnumConverter
	{
		public BooleanValueStylePreferenceConverter() : base(typeof(BooleanValueStylePreference)) { }

		public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
		{
			if (destinationType == typeof(string))
				return (BooleanValueStylePreference)value == BooleanValueStylePreference.Numeric01
					? "数字 0/1"
					: "字符 true/false";

			return base.ConvertTo(context, culture, value, destinationType);
		}

		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			string text = value as string;
			if (text == "数字 0/1" || text == "Numeric01")
				return BooleanValueStylePreference.Numeric01;
			if (text == "字符 true/false" || text == "Characters")
				return BooleanValueStylePreference.Characters;

			return base.ConvertFrom(context, culture, value);
		}
	}

	/// <summary>
	/// General 功能的可配置选项。
	/// 使用标准 DialogPage 属性元数据，兼容 VS 2022 与较新版本的设置属性网格。
	/// </summary>
	[Guid("A4C67610-1A2E-4CF1-9F42-2C2A5A597A5D")]
	public class GeneralOptionsPage : DialogPage
	{
		[Category("General")]
		[DisplayName("启用 Ctrl+/ 快捷注释")]
		[Description("控制是否对所有 *.darkest 文件启用 Ctrl+/ 多行快捷注释与取消注释功能。")]
		[DefaultValue(true)]
		[Browsable(true)]
		public bool EnableCtrlSlashToggleComment { get; set; } = true;

		[Category("Boolean")]
		[DisplayName("字符布尔大小写")]
		[Description("控制自动补全中的字符布尔值使用全小写、全大写或仅首字母大写。默认是全小写。")]
		[DefaultValue(BooleanTextCasePreference.Lowercase)]
		[Browsable(true)]
		public BooleanTextCasePreference BooleanTextCase { get; set; } = BooleanTextCasePreference.Lowercase;

		[Category("Boolean")]
		[DisplayName("双格式布尔值表示形式")]
		[Description("控制同时支持字符布尔和 0/1 的关键字在自动补全中优先使用哪种形式。默认使用字符布尔。")]
		[DefaultValue(BooleanValueStylePreference.Characters)]
		[Browsable(true)]
		public BooleanValueStylePreference BooleanValueStyle { get; set; } = BooleanValueStylePreference.Characters;
	}

	/// <summary>
	/// 提供自动补全读取布尔偏好的统一入口；设置不可用时回退到项目默认值。
	/// </summary>
	internal static class BooleanCompletionPreferenceProvider
	{
		public static List<string> GetCompletionValues(
			List<string> originalValues,
			bool isCharacterBoolean,
			bool supportsNumeric01)
		{
			GeneralOptionsPage options = GetOptions();
			if (supportsNumeric01 && options.BooleanValueStyle == BooleanValueStylePreference.Numeric01)
				return new List<string> { "1", "0" };

			if (!isCharacterBoolean)
				return originalValues;

			string trueValue;
			string falseValue;
			switch (options.BooleanTextCase)
			{
				case BooleanTextCasePreference.Uppercase:
					trueValue = "TRUE";
					falseValue = "FALSE";
					break;
				case BooleanTextCasePreference.TitleCase:
					trueValue = "True";
					falseValue = "False";
					break;
				default:
					trueValue = "true";
					falseValue = "false";
					break;
			}

			return new List<string> { trueValue, falseValue };
		}

		private static GeneralOptionsPage GetOptions()
		{
			try
			{
				For_the_Darkest_DungeonPackage package = For_the_Darkest_DungeonPackage.Instance;
				return package?.GetDialogPage(typeof(GeneralOptionsPage)) as GeneralOptionsPage
					?? new GeneralOptionsPage();
			}
			catch
			{
				// VS 尚未完成设置页初始化时使用默认偏好，不能阻塞补全。
				return new GeneralOptionsPage();
			}
		}
	}
}
