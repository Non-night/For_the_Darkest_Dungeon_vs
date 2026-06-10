using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Windows.Media;

namespace For_the_Darkest_Dungeon.DefinitionDarkest
{
    public static class DarkestContentTypeDefinitions
    {
        // ==========================================
        // 1. Effects 文件格式定义 (.effects.darkest)
        // ==========================================
        [Export]
        [Name("darkest-effect")]
        [BaseDefinition("code")]
        public static ContentTypeDefinition DarkestEffectContentType;

        [Export]
        [FileExtension(".effects.darkest")]
        [ContentType("darkest-effect")]
        public static FileExtensionToContentTypeDefinition DarkestEffectFileExtension;

        // --- Effect 核心关键字格式 ---
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("darkest.effects.keyword.core")]
        [BaseDefinition(PredefinedClassificationTypeNames.MarkupAttribute)]
        internal static ClassificationTypeDefinition DarkestEffectsCoreKeyword;

        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = "darkest.effects.keyword.core")]
        [Name("darkest.effects.keyword.core")]
        [UserVisible(true)]
        internal class DarkestEffectsCoreKeywordFormat : ClassificationFormatDefinition
        {
            public DarkestEffectsCoreKeywordFormat()
            {
                DisplayName = "Darkest Effect 核心关键字";
                IsBold = true;
            }
        }

        // --- Effect 属性关键字格式 ---
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("darkest.effects.keyword.prop")]
        [BaseDefinition(PredefinedClassificationTypeNames.PreprocessorKeyword)]
        internal static ClassificationTypeDefinition DarkestEffectsPropKeyword;

        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = "darkest.effects.keyword.prop")]
        [Name("darkest.effects.keyword.prop")]
        [UserVisible(true)]
        internal class DarkestEffectsPropKeywordFormat : ClassificationFormatDefinition
        {
            public DarkestEffectsPropKeywordFormat()
            {
                DisplayName = "Darkest Effects 普通关键字";
            }
        }

        // ==========================================
        // 2. Info 文件格式定义 (.info.darkest)
        // ==========================================
        [Export]
        [Name("darkest-info")]
        [BaseDefinition("code")]
        public static ContentTypeDefinition DarkestInfoContentType;

        [Export]
        [FileExtension(".info.darkest")]
        [ContentType("darkest-info")]
        public static FileExtensionToContentTypeDefinition DarkestInfoFileExtension;

        // Info 关键字
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("darkest.info.keyword")]
        [BaseDefinition(PredefinedClassificationTypeNames.Type)]
        internal static ClassificationTypeDefinition DarkestInfoKeyword;

        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = "darkest.info.keyword")]
        [Name("darkest.info.keyword")]
		[UserVisible(true)]
		internal class DarkestInfoKeywordFormat : ClassificationFormatDefinition
        {
            public DarkestInfoKeywordFormat()
            {
                DisplayName = "Darkest Info/Art/Override 关键字";
            }
        }

        // 语法错误
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("darkest.error")]
        internal static ClassificationTypeDefinition DarkestErrorKeyword;

        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = "darkest.error")]
        [Name("darkest.error")]
        [UserVisible(true)]
        internal class DarkestError : ClassificationFormatDefinition
        {
            public DarkestError()
            {
                DisplayName = "Darkest Error";
                ForegroundColor = Colors.Yellow;
            }
        }

		// ==========================================
		// 3. Art 文件格式定义 (.art.darkest)
		// ==========================================
		[Export]
		[Name("darkest-art")]
		[BaseDefinition("code")]
		public static ContentTypeDefinition DarkestArtContentType;

		[Export]
		[FileExtension(".art.darkest")]
		[ContentType("darkest-art")]
		public static FileExtensionToContentTypeDefinition DarkestArtFileExtension;

		// ==========================================
		// 4. Override 文件格式定义 (.override.darkest)
		// ==========================================
		[Export]
		[Name("darkest-override")]
		[BaseDefinition("code")]
		public static ContentTypeDefinition DarkestOverrideContentType;

		[Export]
		[FileExtension(".override.darkest")]
		[ContentType("darkest-override")]
		public static FileExtensionToContentTypeDefinition DarkestOverrideFileExtension;
	}
}