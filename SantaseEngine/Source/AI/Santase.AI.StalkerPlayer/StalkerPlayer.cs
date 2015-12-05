namespace Santase.AI.StalkerPlayer
{
    using System.Collections.Generic;
    using System.Linq;
    using Logic;
    using Logic.Cards;
    using Logic.Players;

    using Santase.AI.StalkerPlayer.CardHelpers;
    using Santase.AI.StalkerPlayer.Common;
    using Santase.AI.StalkerPlayer.Common.Constants;
    using Santase.AI.StalkerPlayer.Contracts;

    public class StalkerPlayer : BasePlayer
    {
        private readonly ICardChooser cardChooser;

        private readonly ICardHolder cardHolder;

        private readonly IStalkerHelper stalkerHelper;

        public StalkerPlayer()
        {
            this.cardHolder = new CardHolder();
            this.stalkerHelper = new StalkerHelper(this.cardHolder);
            this.cardChooser = new CardChooser(this.cardHolder, this.stalkerHelper);
        }

        public override string Name => "S.T.A.L.K.E.R";

        public override PlayerAction GetTurn(PlayerTurnContext context)
        {
            var currentGameState = context.State.GetType().Name;

            // If enemy has announce, add other card from announce to enemy card collection.
            if (context.FirstPlayerAnnounce != Announce.None)
            {
                var otherTypeFromAnnounce = context.FirstPlayedCard.Type == CardType.King ? CardType.Queen : CardType.King;
                var otherCardFromAnnounce = new Card(context.FirstPlayedCard.Suit, otherTypeFromAnnounce);

                this.cardHolder.EnemyCards.Add(otherCardFromAnnounce);
                this.cardHolder.AllCards[otherCardFromAnnounce.Suit][otherCardFromAnnounce.Type] = CardStatus.InEnemy;
            }

            // If in final state refresh enemy cards
            if (currentGameState == GameStates.FinalRoundState && context.CardsLeftInDeck == 0 && this.Cards.Count == 6)
            {
                this.cardHolder.RefreshEnemyCards();
                if (context.FirstPlayedCard != null)
                {
                    this.cardHolder.EnemyCards.Remove(context.FirstPlayedCard);
                }
            }

            // Try to change to exchange the bottom trump card from the deck.
            if (this.PlayerActionValidator.IsValid(PlayerAction.ChangeTrump(), context, this.Cards))
            {
                return this.ChangeTrump(context.TrumpCard);
            }

            // In case all requirements are met: close the game.
            if (context.FirstPlayedCard == null &&
                currentGameState == GameStates.MoreThanTwoCardsLeftRoundState &&
                context.State.CanClose &&
                this.stalkerHelper.CanCloseTheGame(context, this.Cards))
            {
                return this.CloseGame();
            }

            Card cardToPlay = context.FirstPlayedCard != null ? this.GetBestCardToRespond(context) : this.GetBestCardToPlayFirst(context);
            return this.PlayCard(cardToPlay);
        }

        public override void EndTurn(PlayerTurnContext context)
        {
            if (this.Cards.Count <= 6)
            {
                this.cardHolder.EnemyCards.Remove(context.FirstPlayedCard);
                this.cardHolder.EnemyCards.Remove(context.SecondPlayedCard);
            }

            // If round ends with 20 or 40 announce one of the players could have not played a card.
            if (context.FirstPlayedCard != null)
            {
                this.cardHolder.AllCards[context.FirstPlayedCard.Suit][context.FirstPlayedCard.Type] = CardStatus.Passed;
            }

            if (context.SecondPlayedCard != null)
            {
                this.cardHolder.AllCards[context.SecondPlayedCard.Suit][context.SecondPlayedCard.Type] = CardStatus.Passed;
            }

            base.EndTurn(context);
        }

        public override void StartRound(ICollection<Card> cards, Card trumpCard, int myTotalPoints, int opponentTotalPoints)
        {
            base.StartRound(cards, trumpCard, myTotalPoints, opponentTotalPoints);
            this.cardHolder.Initialize(this.Cards);
        }

        public override void AddCard(Card card)
        {
            this.cardHolder.AllCards[card.Suit][card.Type] = CardStatus.InStalker;

            //// Something strange happens here. When program stops here the Stalker player still have 5 cards, but UI player already played a card on the context.
            base.AddCard(card);
        }

        public override void EndRound()
        {
            this.cardHolder.EnemyCards.Clear();
            base.EndRound();
        }

        private Card GetBestCardToPlayFirst(PlayerTurnContext context)
        {
            var currentGameState = context.State.GetType().Name;
            var possibleCards = this.PlayerActionValidator.GetPossibleCardsToPlay(context, this.Cards);
            var cardToPlay = this.stalkerHelper.CheckForAnounce(context.TrumpCard.Suit, context.CardsLeftInDeck, currentGameState, this.Cards);
            var cardsByPower = this.Cards.OrderByDescending(c => c.GetValue());

            if (cardToPlay != null)
            {
                return cardToPlay;
            }

            if (currentGameState == GameStates.StartRoundState)
            {
                cardToPlay = this.cardChooser.ChooseCardToPlay(context, possibleCards);

                return cardToPlay;
            }

            if (currentGameState == GameStates.FinalRoundState && context.CardsLeftInDeck == 0)
            {
                foreach (var card in cardsByPower)
                {
                    if (this.stalkerHelper.ContainsLowerCardThan(card, CardStatus.InEnemy) &&
                        !this.stalkerHelper.ContainsGreaterCardThan(card, CardStatus.InEnemy))
                    {
                        return card;
                    }
                }

                var enemyHasTrump = this.cardHolder.EnemyCards.Any(c => c.Suit == context.TrumpCard.Suit);
                var cardThatEnemyHasNotAsSuit = this.stalkerHelper.GetCardWithSuitThatEnemyDoesNotHave(enemyHasTrump, context.TrumpCard.Suit, this.Cards);
                if (cardThatEnemyHasNotAsSuit == null)
                {
                    cardThatEnemyHasNotAsSuit = cardsByPower.LastOrDefault();
                }

                return cardThatEnemyHasNotAsSuit;
            }

            if (currentGameState == GameStates.FinalRoundState)
            {
                var trump = cardsByPower.FirstOrDefault(c => c.Suit == context.TrumpCard.Suit);

                if (trump != null && !this.stalkerHelper.ContainsGreaterCardThan(trump, CardStatus.InDeckOrEnemy))
                {
                    return trump;
                }

                foreach (var card in cardsByPower)
                {
                    if (this.stalkerHelper.ContainsLowerCardThan(card, CardStatus.InDeckOrEnemy))
                    {
                        return card;
                    }
                }

                cardToPlay = cardsByPower.Last();
            }
            else if (currentGameState == GameStates.TwoCardsLeftRoundState)
            {
                cardToPlay =
                    this.Cards.Where(c => c.Suit != context.TrumpCard.Suit).OrderBy(c => c.GetValue()).FirstOrDefault();
            }
            else if (currentGameState == GameStates.MoreThanTwoCardsLeftRoundState)
            {
                cardToPlay = this.cardChooser.ChooseCardToPlay(context, possibleCards);
            }

            return cardToPlay;
        }

        private Card GetBestCardToRespond(PlayerTurnContext context)
        {
            var enemyCard = context.FirstPlayedCard;
            var enemyCardPriority = this.stalkerHelper.GetCardPriority(enemyCard);
            var possibleCards = this.PlayerActionValidator.GetPossibleCardsToPlay(context, this.Cards);
            var trumpSuit = context.TrumpCard.Suit;

            // Use trump to take the enemy card in case it is with higher value
            if ((enemyCardPriority == 2) && enemyCard.Suit != trumpSuit && possibleCards.Any(c => c.Suit == trumpSuit))
            {
                var trump =
                    possibleCards.Where(c => c.Suit == context.TrumpCard.Suit)
                         .OrderBy(this.stalkerHelper.GetCardPriority)
                         .LastOrDefault();
                return trump;
            }

            // Try to take the played enemy card.
            if (possibleCards.Any(c => c.Suit == enemyCard.Suit && c.GetValue() > enemyCard.GetValue()))
            {
                var higherCard =
                    possibleCards.Where(c => c.Suit == enemyCard.Suit).OrderBy(c => c.GetValue()).LastOrDefault();

                return higherCard;
            }

            // Else play the weakest card which is not trump.
            var card = possibleCards.Where(c => c.Suit != trumpSuit).OrderBy(c => c.GetValue()).FirstOrDefault()
                       ?? possibleCards.OrderBy(c => c.GetValue()).FirstOrDefault();

            return card;
        }
    }
}