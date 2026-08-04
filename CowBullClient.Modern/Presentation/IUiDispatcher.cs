namespace CowBullClient.Modern.Presentation;

public interface IUiDispatcher
{
    void Post(Action action);
}
