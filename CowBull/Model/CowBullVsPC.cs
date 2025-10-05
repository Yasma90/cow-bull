using System;
using System.Windows;

namespace CowBull.Model
{
    public class CowBullVsPC : CowBulls
    {
        private CowBulls _cowBulls;

        public PlayedNumber GetPlayedNumber() => new PlayedNumber();
        
        public CowBullVsPC() : base()
        {
            GenerateRandomNumber(4);
        }

        private void GenerateRandomNumber(int numberOfDigits)
        {
            var generatedNumber = "";
            var random = new Random();

            while (generatedNumber.Length != numberOfDigits)
            {
                var randomDigit = random.Next(1, 10);
                var digit = char.Parse(randomDigit.ToString());
                if (!InArray(generatedNumber, digit))
                {
                    generatedNumber += digit;
                }
            }
            NumberToFind = generatedNumber;
        }

        public void PlayMove(string number)
        {
            if (IsValidMove(number))
            {
                if (PlayedNumbers.Count < MAX_ATTEMPTS || !WasNumberFound())
                {
                    CheckMoveAccuracy(number);
                }
            }
        }

        public GameResult GetGameResult()
        {
            if (WasNumberFound())
            {
                return GameResult.Winner;
            }
            return _movesCount == MAX_ATTEMPTS ? GameResult.Loser : GameResult.GameContinues;
        }
    }
}
