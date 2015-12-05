namespace Santase.AI.StalkerPlayer.CardHelpers
{
    using System.Collections.Generic;
    using System.Linq;
    using Common;
    using Common.Constants;
    using Contracts;
    using Logic.Cards;
    using Logic.Players;

    public class StalkerHelper : IStalkerHelper
    {
        private readonly ICardHolder cardHolder;

        public StalkerHelper(ICardHolder holder)
        {
            this.cardHolder = holder;
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

        public bool CanCloseTheGame(PlayerTurnContext context, ICollection<Card> playerCards)
        {
            //// When we have A and 10 from trumps; necessary points && some other winning cards
            //// In the current context we are first player.
            var hasHighTrumps = this.cardHolder.AllCards[context.TrumpCard.Suit][CardType.Ace] == CardStatus.InStalker &&
                                      this.cardHolder.AllCards[context.TrumpCard.Suit][CardType.Ten] == CardStatus.InStalker;
            var has40 = this.cardHolder.AllCards[context.TrumpCard.Suit][CardType.King] == CardStatus.InStalker
                         && this.cardHolder.AllCards[context.TrumpCard.Suit][CardType.Queen] == CardStatus.InStalker;

            var hasEnoughAfterAnounce = context.FirstPlayerRoundPoints > 25;

            var hasNecessaryPoints = playerCards.Sum(c => c.GetValue()) + context.FirstPlayerRoundPoints > 70;

            var sureWiningCards = playerCards.Count(card => !this.ContainsGreaterCardThan(card, CardStatus.InDeckOrEnemy));

            if (has40 && hasEnoughAfterAnounce)
            {
                return true;
            }

            return hasHighTrumps && hasNecessaryPoints && sureWiningCards > 0;
        }

        public Card GetCardWithSuitThatEnemyHasNot(bool enemyHasATrumpCard, CardSuit trumpSuit, ICollection<Card> playerCards)
        {
            if (!enemyHasATrumpCard)
            {
                //// In case enemy does not have any trump cards and we have a trump, should throw a trump;
                var myTrumpCards = playerCards.Where(c => c.Suit == trumpSuit).ToList();
                if (myTrumpCards.Count() > 0)
                {
                    return myTrumpCards.OrderBy(c => c.GetValue()).LastOrDefault();
                }
            }

            var orderedCards = playerCards.OrderBy(c => c.GetValue());
            foreach (var card in orderedCards)
            {
                if (this.cardHolder.EnemyCards.All(c => c.Suit != card.Suit))
                {
                    if (enemyHasATrumpCard)
                    {
                        return playerCards.Where(c => c.Suit == card.Suit).OrderBy(c => c.GetValue()).FirstOrDefault();
                    }

                    return playerCards.Where(c => c.Suit == card.Suit).OrderByDescending(c => c.GetValue()).FirstOrDefault();
                }
            }

            return null;
        }

        public Card CheckForAnounce(CardSuit trumpSuit, int cardsLeftInDeck, string state, ICollection<Card> playerCards)
        {
            if (state == GameStates.StartRoundState)
            {
                return null;
            }

            IList<Card> announcePairs = new List<Card>();

            foreach (var card in playerCards)
            {
                if (card.Type == CardType.King || card.Type == CardType.Queen)
                {
                    var otherTypeForAnnounce = card.Type == CardType.King ? CardType.Queen : CardType.King;
                    var otherCardForAnnounce = new Card(card.Suit, otherTypeForAnnounce);

                    if (this.cardHolder.AllCards[card.Suit][otherTypeForAnnounce] == CardStatus.InStalker)
                    {
                        announcePairs.Add(card);
                        announcePairs.Add(otherCardForAnnounce);
                    }
                }
            }

            if (announcePairs.Count == 0)
            {
                return null;
            }

            //// Check if it's forty.
            if (announcePairs.Any(c => c.Suit == trumpSuit))
            {
                CardStatus cardStatusForTen = this.cardHolder.AllCards[trumpSuit][CardType.Ten];
                CardStatus cardStatusForAce = this.cardHolder.AllCards[trumpSuit][CardType.Ace];

                if ((cardStatusForTen == CardStatus.Passed || cardStatusForTen == CardStatus.InStalker) &&
                        (cardStatusForAce == CardStatus.Passed || cardStatusForAce == CardStatus.InStalker))
                {
                    return new Card(trumpSuit, CardType.King);
                }
                else
                {
                    return new Card(trumpSuit, CardType.Queen);
                }
            }
            else
            {
                var cardToReturn = new Card(announcePairs[0].Suit, announcePairs[0].Type);

                //// They will be ordered in this way: [Q♦ K♦; K♠ Q♠; К♣ Q♣] by pairs: two diamonds, two clubs e.t.c. so incrementation will be i+=2
                for (int i = 0; i < announcePairs.Count; i += 2)
                {
                    CardSuit currentSuit = announcePairs[i].Suit;
                    CardStatus cardStatusForTen = this.cardHolder.AllCards[currentSuit][CardType.Ten];
                    CardStatus cardStatusForAce = this.cardHolder.AllCards[currentSuit][CardType.Ace];

                    //// Return bigger if 10 and A of current Suit is passed or is in us. But it could look suspicious for enemy if we throw a King.
                    if ((cardStatusForTen == CardStatus.Passed || cardStatusForTen == CardStatus.InStalker) &&
                        (cardStatusForAce == CardStatus.Passed || cardStatusForAce == CardStatus.InStalker))
                    {
                        return new Card(currentSuit, CardType.King);
                    }
                    else
                    {
                        cardToReturn = new Card(currentSuit, CardType.Queen);
                    }
                }

                return cardToReturn;
            }
        }

        public bool ContainsGreaterCardThan(Card card, CardStatus status)
        {
            return this.cardHolder.AllCards[card.Suit].Any(c => c.Value == status && new Card(card.Suit, c.Key).GetValue() > card.GetValue());
        }

        public bool ContainsLowerCardThan(Card card, CardStatus status)
        {
            return this.cardHolder.AllCards[card.Suit].Any(c => c.Value == status && new Card(card.Suit, c.Key).GetValue() < card.GetValue());
        }
    }
}
