namespace GameTrainerLauncher.UI.Services;

public interface INavigationService
{
    void NavigateTo(string key);
    event EventHandler<string> Navigated;
}
