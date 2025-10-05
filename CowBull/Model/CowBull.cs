using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace CowBull.Model
{
    public class CowBulls
    {
        public ObservableCollection<PlayedNumber> PlayedNumbers { get; set; }
        public string NumberToFind { get; set; }
        protected const int MAX_ATTEMPTS = 10;
        protected const int NUMBER_OF_DIGITS = 4;
        protected int _movesCount;

        public CowBulls()
        {
            NumberToFind = "1234";
            _movesCount = 0;
        }
        
        public CowBulls(string numberToFind)
        {
            NumberToFind = numberToFind;
            _movesCount = 0;
        }

        public int GetNumberOfDigits()
        {
            return NUMBER_OF_DIGITS;
        }

        public int GetPlayedMovesCount()
        {
            return PlayedNumbers.Count;
        }

        public bool InArray(string array, char element)
        {
            return array.Contains(element);
        }

        public bool WasNumberFound()
        {
            return _movesCount != 0 && PlayedNumbers[PlayedNumbers.Count - 1].Bulls == NUMBER_OF_DIGITS;
        }

        protected void CheckMoveAccuracy(string userNumber)
        {
            if (userNumber == NumberToFind)
            {
                var moveNumber = _movesCount + 1;
                var moveDisplay = _movesCount < 9 ? $"0{moveNumber} - " : $"{moveNumber} - ";
                var currentNumber = new PlayedNumber
                {
                    Number = moveDisplay + userNumber,
                    Bulls = NUMBER_OF_DIGITS,
                    Cows = 0
                };
                PlayedNumbers.Add(currentNumber);
                _movesCount++;
                return;
            }

            int bulls = 0, cows = 0;
            for (int i = 0; i < NUMBER_OF_DIGITS; i++)
            {
                var targetDigit = NumberToFind[i];
                var guessedDigit = userNumber[i];
                if (targetDigit == guessedDigit)
                {
                    bulls++;
                }
                else if (InArray(NumberToFind, guessedDigit))
                {
                    cows++;
                }
            }

            var number = _movesCount + 1;
            var display = _movesCount < 9 ? $"0{number} - " : $"{number} - ";
            var playedNumber = new PlayedNumber
            {
                Number = display + userNumber,
                Bulls = bulls,
                Cows = cows
            };
            PlayedNumbers.Add(playedNumber);
            _movesCount++;
        }

        public bool IsValidMove(string userNumber)
        {
            // userNumber.Length == NUMBER_OF_DIGITS && !RepeatDigit(userNumber);
            if (userNumber.Length != NUMBER_OF_DIGITS)
                return false;

            var seen = new HashSet<char>();
            foreach (char digit in userNumber)
            {
                if (!seen.Add(digit))
                    return false;
            }

            return true;
        }

        private bool RepeatDigit(string userNumber)
        {
            string number = userNumber;
            foreach (char num in number)
            {
                number = number.Substring(1, number.Length);
                if (number.Contains(num))
                    return true;
            }
            return false;
        }

    }

}