using Microsoft.VisualStudio.Editor;

using Microsoft.VisualStudio.Language.Intellisense;

using Microsoft.VisualStudio.OLE.Interop;

using Microsoft.VisualStudio.Text.Editor;

using Microsoft.VisualStudio.TextManager.Interop;

using Microsoft.VisualStudio.Utilities;

using System.ComponentModel.Composition;



namespace For_the_Darkest_Dungeon.Completion

{

    [Export(typeof(IVsTextViewCreationListener))]

    [ContentType("darkest-effect")]

    [TextViewRole(PredefinedTextViewRoles.Editable)]

    internal class TextViewCreationListener : IVsTextViewCreationListener

    {

        [Import]

        internal IVsEditorAdaptersFactoryService AdapterService = null;



        [Import]

        internal ICompletionBroker CompletionBroker = null;



        public void VsTextViewCreated(IVsTextView textViewAdapter)

        {

            ITextView textView = AdapterService.GetWpfTextView(textViewAdapter);

            if (textView == null) return;



            // ?????

            EffectCommandFilter filter = new EffectCommandFilter(textView as IWpfTextView, CompletionBroker);



            IOleCommandTarget next;

            textViewAdapter.AddCommandFilter(filter, out next);

            filter.Next = next;

        }

    }



    [Export(typeof(IVsTextViewCreationListener))]

    [ContentType("darkest-info")]

    [TextViewRole(PredefinedTextViewRoles.Editable)]

    internal class InfoTextViewCreationListener : IVsTextViewCreationListener

    {

        [Import]

        internal IVsEditorAdaptersFactoryService AdapterService = null;



        [Import]

        internal ICompletionBroker CompletionBroker = null;



        public void VsTextViewCreated(IVsTextView textViewAdapter)

        {

            ITextView textView = AdapterService.GetWpfTextView(textViewAdapter);

            if (textView == null) return;



            // ?????

            InfoCommandFilter filter = new InfoCommandFilter(textView as IWpfTextView, CompletionBroker);



            IOleCommandTarget next;

            textViewAdapter.AddCommandFilter(filter, out next);

            filter.Next = next;

        }

    }



    [Export(typeof(IVsTextViewCreationListener))]

    [ContentType("darkest-art")]

    [TextViewRole(PredefinedTextViewRoles.Editable)]

    internal class ArtTextViewCreationListener : IVsTextViewCreationListener

    {

        [Import]

        internal IVsEditorAdaptersFactoryService AdapterService = null;



        [Import]

        internal ICompletionBroker CompletionBroker = null;



        public void VsTextViewCreated(IVsTextView textViewAdapter)

        {

            ITextView textView = AdapterService.GetWpfTextView(textViewAdapter);

            if (textView == null) return;



            // ?????

            ArtCommandFilter filter = new ArtCommandFilter(textView as IWpfTextView, CompletionBroker);



            IOleCommandTarget next;

            textViewAdapter.AddCommandFilter(filter, out next);

            filter.Next = next;

        }

    }



    [Export(typeof(IVsTextViewCreationListener))]

    [ContentType("darkest-override")]

    [TextViewRole(PredefinedTextViewRoles.Editable)]

    internal class OverrideTextViewCreationListener : IVsTextViewCreationListener

    {

        [Import]

        internal IVsEditorAdaptersFactoryService AdapterService = null;



        [Import]

        internal ICompletionBroker CompletionBroker = null;



        public void VsTextViewCreated(IVsTextView textViewAdapter)

        {

            ITextView textView = AdapterService.GetWpfTextView(textViewAdapter);

            if (textView == null) return;



            // ?????

            OverrideCommandFilter filter = new OverrideCommandFilter(textView as IWpfTextView, CompletionBroker);



            IOleCommandTarget next;

            textViewAdapter.AddCommandFilter(filter, out next);

            filter.Next = next;

        }

    }



    [Export(typeof(IVsTextViewCreationListener))]

    [ContentType("darkest-colours")]

    [TextViewRole(PredefinedTextViewRoles.Editable)]

    internal class ColoursTextViewCreationListener : IVsTextViewCreationListener

    {

        [Import]

        internal IVsEditorAdaptersFactoryService AdapterService = null;



        [Import]

        internal ICompletionBroker CompletionBroker = null;



        public void VsTextViewCreated(IVsTextView textViewAdapter)

        {

            ITextView textView = AdapterService.GetWpfTextView(textViewAdapter);

            if (textView == null) return;



            // ?????

            EffectCommandFilter filter = new EffectCommandFilter(textView as IWpfTextView, CompletionBroker);



            IOleCommandTarget next;

            textViewAdapter.AddCommandFilter(filter, out next);

            filter.Next = next;

        }

    }

}

