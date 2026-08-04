namespace CowBullServer.Modern.Presentation;

public interface IUiDispatcher
{
    void Post(Action action);
}
