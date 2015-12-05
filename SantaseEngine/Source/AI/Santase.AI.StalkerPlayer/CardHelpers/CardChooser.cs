﻿namespace Santase.AI.StalkerPlayer.CardHelpers
{
    using System.Collections.Generic;
    using System.Linq;

    using Santase.AI.StalkerPlayer.Common;
    using Santase.AI.StalkerPlayer.Contracts;
    using Santase.Logic.Cards;
    using Santase.Logic.Players;

    public class CardChooser : ICardChooser
    {

        private readonly ICardHolder cardHolder;

        public CardChooser(ICardHolder cardHolder)
        {
            this.cardHolder = cardHolder;
        }

        public Card ChooseCardToPlay(PlayerTurnContext context, ICollection<Card> stalkerCards)
        {
            var trumpSuit = context.TrumpCard.Suit;
            int highestPrioritySuit = 0;
            int priorityValue = int.MaxValue;

            // Get priorities for all suits
            var suitsPriorities = this.GetPriorityForEachSuit(context);

            // Check which suits are available
            var availableSuits = new int[4];
            foreach (var card in stalkerCards)
            {
                int suit = (int)card.Suit;
                availableSuits[suit]++;
            }

            for (int i = 0; i < suitsPriorities.Length; i++)
            {
                if ((int)trumpSuit == i || availableSuits[i] == 0)
                {
                    continue;
                }

                if (suitsPriorities[i] < priorityValue)
                {
                    highestPrioritySuit = i;
                    priorityValue = suitsPriorities[i];
                }
            }

            // Select the cards from the best suit
            var cardsFromBestSuit = stalkerCards.Where(card => card.Suit == (CardSuit)highestPrioritySuit).OrderBy(this.GetCardPriority).ToList();
            var cardsFromTrump = stalkerCards.Where(card => card.Suit == trumpSuit).OrderBy(this.GetCardPriority).ToList();

            if (!context.State.ShouldObserveRules)
            {
                // Take all nontrump cards without Queen and King waiting for announce
                cardsFromBestSuit = stalkerCards.Where(c => c.Suit != trumpSuit && !(this.GetCardPriority(c) == 1 && this.IsCardWaitingForAnnounce(c))).OrderBy(this.GetCardPriority).ToList();
            }

            // Sort cards by its priority
            var cardsToChooseFrom = new List<Card>();
            if (cardsFromBestSuit.Count != 0)
            {
                cardsToChooseFrom = cardsFromBestSuit;
            }
            else
            {
                cardsToChooseFrom = cardsFromTrump;
            }

            if (!context.State.ShouldObserveRules)
            {
                // THIS NUMBER WILL AFFECT THE DECISION OF THE STALKER WHEN IN OPEN STATE
                if (priorityValue > 5)
                {
                    return cardsToChooseFrom.LastOrDefault();
                }

                return cardsToChooseFrom.FirstOrDefault();
            }

            // THIS NUMBER WILL AFFECT THE DECISION OF THE STALKER WHEN IN CLOSED STATE
            if (priorityValue < -1)
            {
                return cardsToChooseFrom.LastOrDefault();
            }

            return cardsToChooseFrom.FirstOrDefault();
        }

        public int GetCardPriority(Card card)
        {
            switch (card.Type)
            {
                case CardType.Nine:
                    return 0;
                case CardType.Jack:
                    return 0;
                case CardType.Queen:
                    return 1;
                case CardType.King:
                    return 1;
                case CardType.Ace:
                    return 2;
                case CardType.Ten:
                    return 2;
                default:
                    return 0;
            }
        }

        private bool IsCardWaitingForAnnounce(Card card)
        {
            CardType otherTypeForAnnounce = card.Type == CardType.King ? CardType.Queen : CardType.King;
            CardStatus statusOfOtherCard = this.cardHolder.AllCards[card.Suit][otherTypeForAnnounce];
            if (statusOfOtherCard == CardStatus.InDeckOrEnemy)
            {
                return true;
            }

            return false;
        }

        private int GetSuitPriority(CardSuit cardSuit)
        {
            return this.cardHolder.AllCards[cardSuit].Count(card => card.Value == CardStatus.Passed || card.Value == CardStatus.InStalker);
        }

        private int GetTrumpPriority(CardSuit trumpSuit, PlayerTurnContext context)
        {
            var countOfTrump = this.GetSuitPriority(trumpSuit);
            if (context.CardsLeftInDeck != 0)
            {
                countOfTrump++;
            }

            return countOfTrump;
        }

        private int[] GetPriorityForEachSuit(PlayerTurnContext context)
        {
            var prioritiesPerSuit = new int[4];
            var trumpSuit = context.TrumpCard.Suit;
            var trumpPriority = this.GetTrumpPriority(trumpSuit, context);

            if (context.State.ShouldObserveRules)
            {
                for (int i = 0; i < 4; i++)
                {
                    if ((int)trumpSuit != i)
                    {
                        prioritiesPerSuit[i] = this.GetSuitPriority((CardSuit)i) - trumpPriority;
                    }
                }
            }
            else
            {
                for (int i = 0; i < prioritiesPerSuit.Length; i++)
                {
                    prioritiesPerSuit[i] = trumpPriority;
                }
            }

            return prioritiesPerSuit;
        }
    }
}