namespace CowBullClient.Modern.ViewModels;

public sealed record ClientAttemptViewModel(
    int AttemptNumber,
    string Guess,
    int Bulls,
    int Cows);
