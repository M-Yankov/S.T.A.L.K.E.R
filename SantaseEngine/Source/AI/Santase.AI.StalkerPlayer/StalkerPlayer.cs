namespace Santase.AI.StalkerPlayer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Logic;
    using Logic.Cards;
    using Logic.Extensions;
    using Logic.Players;

    //// Imagine that we are the second player.
    public class StalkerPlayer : BasePlayer
    {
        private readonly IList<Card> passedCards = new List<Card>(24);
        private IList<Card> enemyCards = new List<Card>(24);
        private IList<Card> cardsLeft = new List<Card>(24);

        private IList<Card> InitizlizeCardsLeft()
        {
            var result = new List<Card>();
            CardSuit[] cardSuits = new[] { CardSuit.Club, CardSuit.Diamond, CardSuit.Heart, CardSuit.Spade };
            CardType[] cardTypes = new[] { CardType.Ace, CardType.Ten, CardType.King, CardType.Queen, CardType.Jack, CardType.Nine };
            for (int i = 0; i < cardSuits.Length; i++)
            {
                for (int j = 0; j < cardTypes.Length; j++)
                {
                    if (!this.Cards.Contains(new Card(cardSuits[i], cardTypes[j])))
                    {
                        result.Add(new Card(cardSuits[i], cardTypes[j]));
                    }

                }
            }

            return result;
        }

        //// All cards are 6 x 4 = new byte[6,4]; on rows are Types, columns are suits. And can mark cells with flags. Easier than many lists.
        public override string Name => "S.T.A.L.K.E.R";

        public override PlayerAction GetTurn(PlayerTurnContext context)
        {
            //// get cards left
            if (context.CardsLeftInDeck == 12)
            {
                this.cardsLeft = this.InitizlizeCardsLeft();
            }

            if (context.FirstPlayerAnnounce != Announce.None)
            {
                //// If enemy has announce, add other card from announce to collection.
                CardType otherTypeFromAnounce = context.FirstPlayedCard.Type == CardType.King ? CardType.Queen : CardType.King;
                var card = new Card(context.FirstPlayedCard.Suit, otherTypeFromAnounce);
                if (!this.Cards.Contains(card))
                {
                    this.enemyCards.Add(card);
                }
            }

            //// Optimize if necessary.
            foreach (Card card in this.Cards)
            {
                if (this.cardsLeft.Contains(card))
                {
                    this.cardsLeft.Remove(card);
                }
            }

            if (this.cardsLeft.Count == 6)
            {
                this.enemyCards = new List<Card>(this.cardsLeft);
                if (context.FirstPlayedCard != null)
                {
                    this.enemyCards.Remove(context.FirstPlayedCard);
                }

                this.cardsLeft.Clear();
            }

            if (this.PlayerActionValidator.IsValid(PlayerAction.ChangeTrump(), context, this.Cards))
            {
                return this.ChangeTrump(context.TrumpCard);
            }

            //// TOdo When To close the game

            return this.SelectBestCard(context);
        }

        public override void EndTurn(PlayerTurnContext context)
        {
            this.cardsLeft.Remove(context.FirstPlayedCard);
            this.cardsLeft.Remove(context.SecondPlayedCard);

            this.passedCards.Add(context.FirstPlayedCard);
            this.passedCards.Add(context.SecondPlayedCard);
            base.EndTurn(context);
        }

        public override void EndRound()
        {
            this.passedCards.Clear();
            this.enemyCards.Clear();
            base.EndRound();
        }

        private PlayerAction SelectBestCard(PlayerTurnContext context)
        {
            //// Copied from dummy just while testing.
            ICollection<Card> possibleCardsToPlay = this.PlayerActionValidator.GetPossibleCardsToPlay(context, this.Cards);
            IEnumerable<Card> shuffledCards = possibleCardsToPlay.Shuffle();
            Card cardToPlay = shuffledCards.First();
            return this.PlayCard(cardToPlay);
        }
    }
}
