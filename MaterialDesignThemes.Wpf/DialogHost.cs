using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MaterialDesignThemes.Wpf
{
    /// <summary>
    /// Defines how a data context is sourced for a dialog if a <see cref="FrameworkElement"/>
    /// is passed as the command parameter when using <see cref="DialogHost.OpenDialogCommand"/>.
    /// </summary>
    public enum DialogHostOpenDialogCommandDataContextSource
    {
        /// <summary>
        /// The data context from the sender element (typically a <see cref="Button"/>) 
        /// is applied to the content.
        /// </summary>
        SenderElement,
        /// <summary>
        /// The data context from the <see cref="DialogHost"/> is applied to the content.
        /// </summary>
        DialogHostInstance,
        /// <summary>
        /// The data context is explicitly set to <c>null</c>.
        /// </summary>
        None
    }

    [TemplatePart(Name = PopupPartName, Type = typeof(Popup))]
    [TemplatePart(Name = PopupPartName, Type = typeof(ContentControl))]
    [TemplatePart(Name = ContentCoverGridName, Type = typeof(Grid))]
    [TemplateVisualState(GroupName = "PopupStates", Name = OpenStateName)]
    [TemplateVisualState(GroupName = "PopupStates", Name = ClosedStateName)]
    public class DialogHost : ContentControl
    {
        public const string PopupPartName = "PART_Popup";
        public const string PopupContentPartName = "PART_PopupContentElement";
        public const string ContentCoverGridName = "PART_ContentCoverGrid";
        public const string OpenStateName = "Open";
        public const string ClosedStateName = "Closed";

        private TaskCompletionSource<object?>? _dialogTaskCompletionSource;

        private Popup? _popup;
        private ContentControl? _popupContentControl;
        private Grid? _contentCoverGrid;
        private IInputElement? _restoreFocusDialogClose;

        static DialogHost()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DialogHost), new FrameworkPropertyMetadata(typeof(DialogHost)));
        }

        #region Show overloads
        internal async Task<object?> ShowInternal(object content, DialogOpenedEventHandler? openedEventHandler, DialogClosingEventHandler? closingEventHandler)
        {
            if (IsOpen)
                throw new InvalidOperationException("DialogHost is already open.");

            _dialogTaskCompletionSource = new TaskCompletionSource<object?>();

            AssertTargetableContent();

            if (content != null)
                DialogContent = content;

            SetCurrentValue(IsOpenProperty, true);

            object? result = await _dialogTaskCompletionSource.Task;

            return result;
        }

        #endregion

        public DialogHost() { }

        public static readonly DependencyProperty IdentifierProperty = DependencyProperty.Register(
            nameof(Identifier), typeof(object), typeof(DialogHost), new PropertyMetadata(default(object)));

        /// <summary>
        /// Identifier which is used in conjunction with <see cref="Show(object)"/> to determine where a dialog should be shown.
        /// </summary>
        public object? Identifier
        {
            get => GetValue(IdentifierProperty);
            set => SetValue(IdentifierProperty, value);
        }

        public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
            nameof(IsOpen), typeof(bool), typeof(DialogHost), new FrameworkPropertyMetadata(default(bool), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, IsOpenPropertyChangedCallback));

        private static void IsOpenPropertyChangedCallback(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs)
        {
            var dialogHost = (DialogHost)dependencyObject;

            if (dialogHost._popupContentControl != null)
                ValidationAssist.SetSuppress(dialogHost._popupContentControl, !dialogHost.IsOpen);
            VisualStateManager.GoToState(dialogHost, dialogHost.SelectState(), !TransitionAssist.GetDisableTransitions(dialogHost));

            if (dialogHost.IsOpen)
            {
            }
            else
            {
                object? closeParameter = null;
                if (dialogHost.CurrentSession is { } session)
                {
                    closeParameter = session.CloseParameter;
                    session.IsEnded = true;
                    dialogHost.CurrentSession = null;
                }

                //NB: _dialogTaskCompletionSource is only set in the case where the dialog is shown with Show
                dialogHost._dialogTaskCompletionSource?.TrySetResult(closeParameter);

                // Don't attempt to Invoke if _restoreFocusDialogClose hasn't been assigned yet. Can occur
                // if the MainWindow has started up minimized. Even when Show() has been called, this doesn't
                // seem to have been set.
                dialogHost.Dispatcher.InvokeAsync(() => dialogHost._restoreFocusDialogClose?.Focus(), DispatcherPriority.Input);

                return;
            }

            dialogHost.CurrentSession = new DialogSession(dialogHost);
            var window = Window.GetWindow(dialogHost);
            dialogHost._restoreFocusDialogClose = window != null ? FocusManager.GetFocusedElement(window) : null;

            dialogHost.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                CommandManager.InvalidateRequerySuggested();
                UIElement? child = dialogHost.FocusPopup();

                if (child != null)
                {
                    //https://github.com/ButchersBoy/MaterialDesignInXamlToolkit/issues/187
                    //totally not happy about this, but on immediate validation we can get some weird looking stuff...give WPF a kick to refresh...
                    Task.Delay(300).ContinueWith(t => child.Dispatcher.BeginInvoke(new Action(() => child.InvalidateVisual())));
                }
            }));
        }

        /// <summary>
        /// Returns a DialogSession for the currently open dialog for managing it programmatically. If no dialog is open, CurrentSession will return null
        /// </summary>
        public DialogSession? CurrentSession { get; private set; }

        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        public static readonly DependencyProperty DialogContentProperty = DependencyProperty.Register(
            nameof(DialogContent), typeof(object), typeof(DialogHost), new PropertyMetadata(default(object)));

        public object? DialogContent
        {
            get => GetValue(DialogContentProperty);
            set => SetValue(DialogContentProperty, value);
        }

        public static readonly DependencyProperty DialogContentTemplateProperty = DependencyProperty.Register(
            nameof(DialogContentTemplate), typeof(DataTemplate), typeof(DialogHost), new PropertyMetadata(default(DataTemplate)));

        public DataTemplate? DialogContentTemplate
        {
            get => (DataTemplate?)GetValue(DialogContentTemplateProperty);
            set => SetValue(DialogContentTemplateProperty, value);
        }

        public static readonly DependencyProperty DialogContentTemplateSelectorProperty = DependencyProperty.Register(
            nameof(DialogContentTemplateSelector), typeof(DataTemplateSelector), typeof(DialogHost), new PropertyMetadata(default(DataTemplateSelector)));

        public DataTemplateSelector? DialogContentTemplateSelector
        {
            get => (DataTemplateSelector?)GetValue(DialogContentTemplateSelectorProperty);
            set => SetValue(DialogContentTemplateSelectorProperty, value);
        }

        public static readonly DependencyProperty DialogContentStringFormatProperty = DependencyProperty.Register(
            nameof(DialogContentStringFormat), typeof(string), typeof(DialogHost), new PropertyMetadata(default(string)));

        public string? DialogContentStringFormat
        {
            get => (string?)GetValue(DialogContentStringFormatProperty);
            set => SetValue(DialogContentStringFormatProperty, value);
        }

        public static readonly DependencyProperty DialogMarginProperty = DependencyProperty.Register(
            "DialogMargin", typeof(Thickness), typeof(DialogHost), new PropertyMetadata(default(Thickness)));

        public Thickness DialogMargin
        {
            get => (Thickness)GetValue(DialogMarginProperty);
            set => SetValue(DialogMarginProperty, value);
        }

        public static readonly DependencyProperty OpenDialogCommandDataContextSourceProperty = DependencyProperty.Register(
            nameof(OpenDialogCommandDataContextSource), typeof(DialogHostOpenDialogCommandDataContextSource), typeof(DialogHost), new PropertyMetadata(default(DialogHostOpenDialogCommandDataContextSource)));

        /// <summary>
        /// Defines how a data context is sourced for a dialog if a <see cref="FrameworkElement"/>
        /// is passed as the command parameter when using <see cref="DialogHost.OpenDialogCommand"/>.
        /// </summary>
        public DialogHostOpenDialogCommandDataContextSource OpenDialogCommandDataContextSource
        {
            get => (DialogHostOpenDialogCommandDataContextSource)GetValue(OpenDialogCommandDataContextSourceProperty);
            set => SetValue(OpenDialogCommandDataContextSourceProperty, value);
        }

        public static readonly DependencyProperty CloseOnClickAwayProperty = DependencyProperty.Register(
            "CloseOnClickAway", typeof(bool), typeof(DialogHost), new PropertyMetadata(default(bool)));

        /// <summary>
        /// Indicates whether the dialog will close if the user clicks off the dialog, on the obscured background.
        /// </summary>
        public bool CloseOnClickAway
        {
            get => (bool)GetValue(CloseOnClickAwayProperty);
            set => SetValue(CloseOnClickAwayProperty, value);
        }

        public static readonly DependencyProperty CloseOnClickAwayParameterProperty = DependencyProperty.Register(
            "CloseOnClickAwayParameter", typeof(object), typeof(DialogHost), new PropertyMetadata(default(object)));

        /// <summary>
        /// Parameter to provide to close handlers if an close due to click away is instigated.
        /// </summary>
        public object? CloseOnClickAwayParameter
        {
            get => GetValue(CloseOnClickAwayParameterProperty);
            set => SetValue(CloseOnClickAwayParameterProperty, value);
        }

        public static readonly DependencyProperty DialogThemeProperty =
            DependencyProperty.Register(nameof(DialogTheme), typeof(BaseTheme), typeof(DialogHost), new PropertyMetadata(default(BaseTheme)));

        /// <summary>
        /// Set the theme (light/dark) for the dialog.
        /// </summary>
        public BaseTheme DialogTheme
        {
            get => (BaseTheme)GetValue(DialogThemeProperty);
            set => SetValue(DialogThemeProperty, value);
        }

        public static readonly DependencyProperty PopupStyleProperty = DependencyProperty.Register(
            nameof(PopupStyle), typeof(Style), typeof(DialogHost), new PropertyMetadata(default(Style)));

        public Style? PopupStyle
        {
            get => (Style?)GetValue(PopupStyleProperty);
            set => SetValue(PopupStyleProperty, value);
        }

        public static readonly DependencyProperty OverlayBackgroundProperty = DependencyProperty.Register(
            nameof(OverlayBackground), typeof(Brush), typeof(DialogHost), new PropertyMetadata(Brushes.Black));

        /// <summary>
        /// Represents the overlay brush that is used to dim the background behind the dialog
        /// </summary>
        public Brush? OverlayBackground
        {
            get => (Brush?)GetValue(OverlayBackgroundProperty);
            set => SetValue(OverlayBackgroundProperty, value);
        }

        public override void OnApplyTemplate()
        {
            if (_contentCoverGrid != null)
                _contentCoverGrid.MouseLeftButtonUp -= ContentCoverGridOnMouseLeftButtonUp;

            _popup = GetTemplateChild(PopupPartName) as Popup;
            _popupContentControl = GetTemplateChild(PopupContentPartName) as ContentControl;
            _contentCoverGrid = GetTemplateChild(ContentCoverGridName) as Grid;

            if (_contentCoverGrid != null)
                _contentCoverGrid.MouseLeftButtonUp += ContentCoverGridOnMouseLeftButtonUp;

            VisualStateManager.GoToState(this, SelectState(), false);

            base.OnApplyTemplate();
        }

        internal void AssertTargetableContent()
        {
            var existingBinding = BindingOperations.GetBindingExpression(this, DialogContentProperty);
            if (existingBinding != null)
                throw new InvalidOperationException(
                    "Content cannot be passed to a dialog via the OpenDialog if DialogContent already has a binding.");
        }

        internal void InternalClose(object? parameter)
        {
            SetCurrentValue(IsOpenProperty, false);
        }

        /// <summary>
        /// Attempts to focus the content of a popup.
        /// </summary>
        /// <returns>The popup content.</returns>
        internal UIElement? FocusPopup()
        {
            var child = _popup?.Child ?? _popupContentControl;
            if (child is null) return null;

            CommandManager.InvalidateRequerySuggested();
            var focusable = child.VisualDepthFirstTraversal().OfType<UIElement>().FirstOrDefault(ui => ui.Focusable && ui.IsVisible);
            focusable?.Dispatcher.InvokeAsync(() =>
            {
                if (!focusable.Focus()) return;
                focusable.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            }, DispatcherPriority.Background);

            return child;
        }

        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window != null && !window.IsActive)
                window.Activate();
            base.OnPreviewMouseDown(e);
        }

        private void ContentCoverGridOnMouseLeftButtonUp(object sender, MouseButtonEventArgs mouseButtonEventArgs)
        {
            if (CloseOnClickAway && CurrentSession != null)
                InternalClose(CloseOnClickAwayParameter);
        }

        private string SelectState()
        {
            return IsOpen ? OpenStateName : ClosedStateName;
        }

    }
}
