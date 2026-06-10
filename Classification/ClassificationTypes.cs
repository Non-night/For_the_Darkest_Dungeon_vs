using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Windows.Media;

namespace For_the_Darkest_Dungeon.Classification
{
    internal static class ClassificationDefinitions
    {
        // 1. 定义数值颜色 (整数, 浮点, 百分比)
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("darkest.number")]
		[BaseDefinition(PredefinedClassificationTypeNames.Number)]
		internal static ClassificationTypeDefinition DarkestNumber;

        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = "darkest.number")]
        [Name("darkest.number")]
        [UserVisible(true)]
        internal class DarkestNumberFormat : ClassificationFormatDefinition
        {
            public DarkestNumberFormat()
            {
                DisplayName = "Darkest Effect 数字";
            }
        }

        // 2. 定义起始符颜色 (effect:)
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("darkest.header")]
        internal static ClassificationTypeDefinition DarkestHeader;

        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = "darkest.header")]
        [Name("darkest.header")]
        [UserVisible(true)]
        internal class DarkestHeaderFormat : ClassificationFormatDefinition
        {
            public DarkestHeaderFormat()
            {
                DisplayName = "Darkest Effect 开头";
                ForegroundColor = Colors.SteelBlue;
                IsBold = true;
            }
        }

        // 3. 定义未加引号字符串颜色
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("darkest.unquoted")]
		[BaseDefinition(PredefinedClassificationTypeNames.Text)]
		internal static ClassificationTypeDefinition DarkestUnquoted;

        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = "darkest.unquoted")]
        [Name("darkest.unquoted")]
        [UserVisible(true)]
        internal class DarkestUnquotedFormat : ClassificationFormatDefinition
        {
            public DarkestUnquotedFormat()
            {
                DisplayName = "Darkest 无引号字符串";
            }
        }

        // 4. 定义带引号字符串颜色
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("darkest.string")]
		[BaseDefinition(PredefinedClassificationTypeNames.String)]
		internal static ClassificationTypeDefinition DarkestString;

        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = "darkest.string")]
        [Name("darkest.string")]
        [UserVisible(true)]
        internal class DarkestStringFormat : ClassificationFormatDefinition
        {
            public DarkestStringFormat()
            {
                DisplayName = "Darkest 带引号字符串";
            }
        }

        // 5. 定义布尔颜色
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("darkest.bool")]
		[BaseDefinition(PredefinedClassificationTypeNames.Text)]
		internal static ClassificationTypeDefinition DarkestBool;

        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = "darkest.bool")]
        [Name("darkest.bool")]
        [UserVisible(true)]
        internal class DarkestBoolFormat : ClassificationFormatDefinition
        {
            public DarkestBoolFormat()
            {
                DisplayName = "Darkest 布尔";
            }
        }

        // 6. 定义注释颜色
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("darkest.comment")]
		[BaseDefinition(PredefinedClassificationTypeNames.Comment)]
		internal static ClassificationTypeDefinition DarkestComment;

        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = "darkest.comment")]
        [Name("darkest.comment")]
        [UserVisible(true)]
        internal class DarkestCommentFormat : ClassificationFormatDefinition
        {
            public DarkestCommentFormat()
            {
                DisplayName = "Darkest 注释";
            }
        }

        // 单独调整特定颜色
        // 流血
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("darkest.effects.keyword.bleed")]
        internal static ClassificationTypeDefinition DarkestBleedKeyword;

        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = "darkest.effects.keyword.bleed")]
        [Name("darkest.effects.keyword.bleed")]
        [UserVisible(true)]
        internal class DarkestBleedKeywordFormat : ClassificationFormatDefinition
        {
            public DarkestBleedKeywordFormat()
            {
                DisplayName = "Darkest Bleed Keyword (.bleed)";
                ForegroundColor = Color.FromRgb(0xb1, 0x00, 0x00);
                IsBold = false;
            }
        }
        // 腐蚀
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("darkest.effects.keyword.poison")]
        internal static ClassificationTypeDefinition DarkestPoisonKeyword;

        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = "darkest.effects.keyword.poison")]
        [Name("darkest.effects.keyword.poison")]
        [UserVisible(true)]
        internal class DarkestPoisonKeywordFormat : ClassificationFormatDefinition
        {
            public DarkestPoisonKeywordFormat()
            {
                DisplayName = "Darkest Poison Keyword (.poison)";
                ForegroundColor = Color.FromRgb(0xbd, 0xc2, 0x41);
                IsBold = false;
            }
        }
        // 治疗
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("darkest.effects.keyword.heal")]
        internal static ClassificationTypeDefinition DarkestHealKeyword;

        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = "darkest.effects.keyword.heal")]
        [Name("darkest.effects.keyword.heal")]
        [UserVisible(true)]
        internal class DarkestHealKeywordFormat : ClassificationFormatDefinition
        {
            public DarkestHealKeywordFormat()
            {
                DisplayName = "Darkest Heal Keyword (.heal)";
                ForegroundColor = Color.FromRgb(0x87, 0xc2, 0x41);
                IsBold = false;
            }
        }
        // 眩晕
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("darkest.effects.keyword.stun")]
        internal static ClassificationTypeDefinition DarkestStunKeyword;

        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = "darkest.effects.keyword.stun")]
        [Name("darkest.effects.keyword.stun")]
        [UserVisible(true)]
        internal class DarkestStunKeywordFormat : ClassificationFormatDefinition
        {
            public DarkestStunKeywordFormat()
            {
                DisplayName = "Darkest Stun Keyword (.stun)";
                ForegroundColor = Color.FromRgb(0xc9, 0x9c, 0x45);
                IsBold = false;
            }
        }
        // 反击
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("darkest.effects.keyword.riposte")]
        internal static ClassificationTypeDefinition DarkestRiposteKeyword;

        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = "darkest.effects.keyword.riposte")]
        [Name("darkest.effects.keyword.riposte")]
        [UserVisible(true)]
        internal class DarkestRiposteKeywordFormat : ClassificationFormatDefinition
        {
            public DarkestRiposteKeywordFormat()
            {
                DisplayName = "Darkest Riposte Keyword (.riposte)";
                ForegroundColor = Color.FromRgb(0xc3, 0x63, 0x0f);
                IsBold = false;
            }
        }
        // buff
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("darkest.effects.keyword.buff")]
        internal static ClassificationTypeDefinition DarkestBuffKeyword;

        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = "darkest.effects.keyword.buff")]
        [Name("darkest.effects.keyword.buff")]
        [UserVisible(true)]
        internal class DarkestBuffKeywordFormat : ClassificationFormatDefinition
        {
            public DarkestBuffKeywordFormat()
            {
                DisplayName = "Darkest Buff Keyword (.buff)";
                ForegroundColor = Color.FromRgb(0x5e, 0xc9, 0xd6);
                IsBold = false;
            }
        }
        // kill
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("darkest.effects.keyword.kill")]
        internal static ClassificationTypeDefinition DarkestKillKeyword;

        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = "darkest.effects.keyword.kill")]
        [Name("darkest.effects.keyword.kill")]
        [UserVisible(true)]
        internal class DarkestKillKeywordFormat : ClassificationFormatDefinition
        {
            public DarkestKillKeywordFormat()
            {
                DisplayName = "Darkest Kill Keyword (.kill)";
                ForegroundColor = Colors.Red;
                IsBold = false;
            }
        }
        // summon
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("darkest.effects.keyword.summon")]
        internal static ClassificationTypeDefinition DarkestSummonKeyword;

        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = "darkest.effects.keyword.summon")]
        [Name("darkest.effects.keyword.summon")]
        [UserVisible(true)]
        internal class DarkestSummonKeywordFormat : ClassificationFormatDefinition
        {
            public DarkestSummonKeywordFormat()
            {
                DisplayName = "Darkest Summon Keyword (.summon)";
                ForegroundColor = Color.FromRgb(0x7f, 0xff, 0xd4);
                IsBold = false;
            }
        }
    }
}