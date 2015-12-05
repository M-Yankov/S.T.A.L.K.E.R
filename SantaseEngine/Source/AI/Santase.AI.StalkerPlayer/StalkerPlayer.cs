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

        private readonly ICardHelper cardHelper;

        public StalkerPlayer()
        {
            this.cardHelper = new CardHelper();
            this.cardHolder = new CardHolder();
            this.cardChooser = new CardChooser(this.cardHolder, this.cardHelper);
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
                this.CanCloseTheGame(context))
            {
                return this.CloseGame();
            }

            return this.PlayCard(context.FirstPlayedCard != null ? this.GetBestCardToRespond(context) : this.GetBestCardToPlayFirst(context));
        }

        private Card GetBestCardToRespond(PlayerTurnContext context)
        {
            var enemyCard = context.FirstPlayedCard;
            var enemyCardPriority = this.cardHelper.GetCardPriority(enemyCard);
            var possibleCards = this.PlayerActionValidator.GetPossibleCardsToPlay(context, this.Cards);
            var trumpSuit = context.TrumpCard.Suit;

            // Use trump to take the enemy card in case it is with higher value
            if (enemyCardPriority == 2 && enemyCard.Suit != trumpSuit && possibleCards.Any(c => c.Suit == trumpSuit))
            {
                // TODO: Change the trump card used to take enemy card
                var trump =
                   possibleCards.Where(c => c.Suit == context.TrumpCard.Suit)
                        .OrderBy(this.cardHelper.GetCardPriority)
                        .FirstOrDefault();
                return trump;
            }

            // Try to take the played enemy card.
            if (possibleCards.Any(c => c.Suit == enemyCard.Suit && c.GetValue() > enemyCard.GetValue()))
            {
                // TODO: Do not take weak cards
                var higherCard =
                    possibleCards.Where(c => c.Suit == enemyCard.Suit).OrderBy(c => c.GetValue()).LastOrDefault();

                return higherCard;
            }

            // Else play the weakest card which is not trump.
            var card = possibleCards.Where(c => c.Suit != trumpSuit).OrderBy(c => c.GetValue()).FirstOrDefault()
                       ?? possibleCards.OrderBy(c => c.GetValue()).FirstOrDefault();

            return card;
        }

        public override void EndTurn(PlayerTurnContext context)
        {
            if (this.Cards.Count <= 6)
            {
                this.cardHolder.EnemyCards.Remove(context.FirstPlayedCard);
                this.cardHolder.EnemyCards.Remove(context.SecondPlayedCard);
            }

            // If round ends with 20 or 40 announce one of the players could have not played a card
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

        // TODO: Refactor to return card not PlayerAction
        private Card GetBestCardToPlayFirst(PlayerTurnContext context)
        {
            var currentGameState = context.State.GetType().Name;
            var possibleCards = this.PlayerActionValidator.GetPossibleCardsToPlay(context, this.Cards);
            var cardToPlay = this.CheckForAnounce(context.TrumpCard.Suit, context.CardsLeftInDeck, currentGameState);

            if (cardToPlay != null)
            {
                return cardToPlay;
            }

            if (currentGameState == GameStates.StartRoundState)
            {
                var smallestCard = this.cardChooser.ChooseCardToPlay(context, possibleCards);

                return smallestCard;
            }
            else if (currentGameState == GameStates.FinalRoundState && context.CardsLeftInDeck == 0)
            {
                var cardsByPower = this.Cards.OrderByDescending(c => c.GetValue());
                foreach (var card in cardsByPower)
                {
                    if (this.EnemyContainsLowerCardThan(card) && !this.EnemyContainsGreaterCardThan(card))
                    {
                        return card;
                    }
                }

                bool enemyHasTrump = this.cardHolder.EnemyCards.Any(c => c.Suit == context.TrumpCard.Suit);
                Card cardThatEnemyHasNotAsSuit = this.GetCardWithSuitThatEnemyHasNot(enemyHasTrump, context.TrumpCard.Suit);
                if (cardThatEnemyHasNotAsSuit == null)
                {
                    cardThatEnemyHasNotAsSuit = cardsByPower.LastOrDefault();
                    //cardThatEnemyHasNotAsSuit = this.ChooseCardToPlay(this.allCards, context, possibleCards);
                }

                return cardThatEnemyHasNotAsSuit;
            }
            else if (currentGameState == GameStates.FinalRoundState)
            {
                IEnumerable<Card> orderedByPower = this.Cards.OrderByDescending(c => c.GetValue());
                Card trump = orderedByPower.FirstOrDefault(c => c.Suit == context.TrumpCard.Suit);

                if (trump != null && !this.HasGreatherNonPassedCardThan(trump))
                {
                    return trump;
                }

                foreach (var card in orderedByPower)
                {
                    if (this.HasSmallerNonPassedCardThan(card))
                    {
                        return card;
                    }
                }

                cardToPlay = orderedByPower.Last();
            }
            else if (currentGameState == GameStates.TwoCardsLeftRoundState)
            {
                cardToPlay =
                    this.Cards.Where(c => c.Suit != context.TrumpCard.Suit).OrderBy(c => c.GetValue()).FirstOrDefault();
            }

            // TODO: Extract game closing to other method
            else if (currentGameState == GameStates.MoreThanTwoCardsLeftRoundState)
            {
                cardToPlay = this.cardChooser.ChooseCardToPlay(context, possibleCards);
            }

            return cardToPlay;
        }

        private bool CanCloseTheGame(PlayerTurnContext context)
        {
            //// When we have A and 10 from trumps; necessary points && some other winning cards
            //// In the current context we are first player.
            var hasHighTrumps = this.cardHolder.AllCards[context.TrumpCard.Suit][CardType.Ace] == CardStatus.InStalker &&
                                      this.cardHolder.AllCards[context.TrumpCard.Suit][CardType.Ten] == CardStatus.InStalker;
            var has40 = this.cardHolder.AllCards[context.TrumpCard.Suit][CardType.King] == CardStatus.InStalker
                         && this.cardHolder.AllCards[context.TrumpCard.Suit][CardType.Queen] == CardStatus.InStalker;

            var hasEnoughAfterAnounce = context.FirstPlayerRoundPoints > 25;

            var hasNecessaryPoints = this.Cards.Sum(c => c.GetValue()) + context.FirstPlayerRoundPoints > 70;

            var sureWiningCards = this.Cards.Count(card => !this.HasGreatherNonPassedCardThan(card));

            if (has40 && hasEnoughAfterAnounce)
            {
                return true;
            }

            return hasHighTrumps && hasNecessaryPoints && sureWiningCards > 0;
        }

        private Card GetCardWithSuitThatEnemyHasNot(bool enemyHasATrumpCard, CardSuit trumpSuit)
        {
            if (!enemyHasATrumpCard)
            {
                //// In case enemy does not have any trump cards and we have a trump, should throw a trump;
                var myTrumpCards = this.Cards.Where(c => c.Suit == trumpSuit).ToList();
                if (myTrumpCards.Count() > 0)
                {
                    return myTrumpCards.OrderBy(c => c.GetValue()).LastOrDefault();
                }
            }

            var orderedCards = this.Cards.OrderBy(c => c.GetValue());
            foreach (var card in orderedCards)
            {
                if (this.cardHolder.EnemyCards.All(c => c.Suit != card.Suit))
                {
                    if (enemyHasATrumpCard)
                    {
                        return this.Cards.Where(c => c.Suit == card.Suit).OrderBy(c => c.GetValue()).First();
                    }

                    return this.Cards.Where(c => c.Suit == card.Suit).OrderByDescending(c => c.GetValue()).First();
                }
            }

            return null;
        }

        // TODO: Refactor in one method
        private bool EnemyContainsLowerCardThan(Card card)
        {
            return this.cardHolder.AllCards[card.Suit].Any(c => c.Value == CardStatus.InEnemy && new Card(card.Suit, c.Key).GetValue() < card.GetValue());
        }

        private bool EnemyContainsGreaterCardThan(Card card)
        {
            return this.cardHolder.AllCards[card.Suit].Any(c => c.Value == CardStatus.InEnemy && new Card(card.Suit, c.Key).GetValue() > card.GetValue());
        }

        private bool HasGreatherNonPassedCardThan(Card card)
        {
            return this.cardHolder.AllCards[card.Suit].Any(c => c.Value == CardStatus.InDeckOrEnemy && new Card(card.Suit, c.Key).GetValue() > card.GetValue());
        }

        private bool HasSmallerNonPassedCardThan(Card card)
        {
            return this.cardHolder.AllCards[card.Suit].Any(c => c.Value == CardStatus.InDeckOrEnemy && new Card(card.Suit, c.Key).GetValue() < card.GetValue());
        }

        private Card CheckForAnounce(CardSuit trumpSuit, int cardsLeftInDeck, string state)
        {
            if (state == GameStates.StartRoundState)
            {
                return null;
            }

            IList<Card> announcePairs = new List<Card>();

            foreach (var card in this.Cards)
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
    }
}