using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CodeTutor.Desktop.ViewModels.Panels;

namespace CodeTutor.Desktop.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public CameraPanelViewModel CameraPanel { get; } = new();
    public QuestionPanelViewModel QuestionPanel { get; } = new();
    public SolutionPanelViewModel SolutionPanel { get; } = new();
    public FeedbackPanelViewModel FeedbackPanel { get; } = new();
    public HistoryViewModel History { get; } = new();
    public SettingsBarViewModel Settings { get; } = new();

    [ObservableProperty]
    private string _statusText = "就绪";

    public MainWindowViewModel()
    {
        QuestionPanel.CaptureAndOcrCommand = new RelayCommand(() => StatusText = "截取并识别（待实现）");
        QuestionPanel.UndoCaptureCommand = new RelayCommand(() => StatusText = "撤销（待实现）");
        QuestionPanel.ClearSessionCommand = new RelayCommand(() => StatusText = "全部清空（待实现）");
        SolutionPanel.SolveTextCommand = new RelayCommand(() => StatusText = "文字解答（待实现）");
        SolutionPanel.SolveImagesCommand = new RelayCommand(() => StatusText = "图片直接解答（待实现）");
        Settings.SaveAndTestCommand = new RelayCommand(() => StatusText = "API 测试（待实现）");
        FeedbackPanel.SendFollowUpCommand = new RelayCommand(() => StatusText = "追问（待实现）");
        History.ShowHistoryCommand = new RelayCommand(() => StatusText = "历史（待实现）");
        CameraPanel.RefreshCameraCommand = new RelayCommand(() => StatusText = "刷新摄像头（待实现）");
    }
}
