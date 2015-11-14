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
        private readonly List<Card> passedCards = new List<Card>(24);
        private readonly List<Card> cardsLeft = new List<Card>(24);
        private List<Card> enemyCards = new List<Card>(24);

        //// All cards are 6 x 4 = new byte[6,4]; on rows are Types, columns are suits. And can mark cells with flags. Easier than many lists.

        public override string Name => "S.T.A.L.K.E.R";

        public override PlayerAction GetTurn(PlayerTurnContext context)
        {
            if (this.PlayerActionValidator.IsValid(PlayerAction.ChangeTrump(), context, this.Cards))
            {
                return this.ChangeTrump(context.TrumpCard);
            }

            //// TOdo When To close the game

            return this.SelectBestCard(context);
        }

        public override void EndTurn(PlayerTurnContext context)
        {
            if (context.FirstPlayerAnnounce != Announce.None)
            {
                //// If enemy has announce, add other card to collection.
                var otherTypeFromAnounce = context.FirstPlayedCard.Type == CardType.King ? CardType.Queen : CardType.King;
                var card = new Card(context.FirstPlayedCard.Suit, otherTypeFromAnounce);
                if (!this.Cards.Contains(card))
                {
                    this.enemyCards.Add(card);
                }
            }

            if (context.State.ShouldObserveRules)
            {
                this.enemyCards = new List<Card>(this.cardsLeft);
            }

            this.passedCards.Add(context.FirstPlayedCard);
            this.passedCards.Add(context.SecondPlayedCard);
            base.EndTurn(context);
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
