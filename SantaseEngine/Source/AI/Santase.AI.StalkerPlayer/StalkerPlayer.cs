namespace Santase.AI.StalkerPlayer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Logic;
    using Logic.Cards;
    using Logic.Extensions;
    using Logic.Players;

    using Santase.AI.StalkerPlayer.CardHelpers;
    using Santase.AI.StalkerPlayer.Common;
    using Santase.AI.StalkerPlayer.Common.Constants;

    public class StalkerPlayer : BasePlayer
    {
        private readonly CardSuit[] cardSuits = new[] { CardSuit.Club, CardSuit.Diamond, CardSuit.Heart, CardSuit.Spade };
        private readonly CardType[] cardTypes = new[] { CardType.Ace, CardType.Ten, CardType.King, CardType.Queen, CardType.Jack, CardType.Nine };
        private HashSet<Card> enemyCards = new HashSet<Card>();
        private Dictionary<CardSuit, Dictionary<CardType, CardStatus>> allCards;
        private EnemyPlayerStatistics enemyStats;
        private readonly CardChooser cardChooser;

        public StalkerPlayer()
        {
            this.enemyStats = new EnemyPlayerStatistics();

            this.allCards = new Dictionary<CardSuit, Dictionary<CardType, CardStatus>>();
            this.allCards.Add(CardSuit.Club, new Dictionary<CardType, CardStatus>());
            this.allCards.Add(CardSuit.Diamond, new Dictionary<CardType, CardStatus>());
            this.allCards.Add(CardSuit.Heart, new Dictionary<CardType, CardStatus>());
            this.allCards.Add(CardSuit.Spade, new Dictionary<CardType, CardStatus>());

            this.cardChooser = new CardChooser(this.allCards);
        }

        public override string Name => "S.T.A.L.K.E.R";

        public override PlayerAction GetTurn(PlayerTurnContext context)
        {
            var currentGameState = context.State.GetType().Name;

            //// get cards left
            if (context.FirstPlayerAnnounce != Announce.None)
            {
                //// If enemy has announce, add other card from announce to enemy card collection.
                CardType otherTypeFromAnnounce = context.FirstPlayedCard.Type == CardType.King ? CardType.Queen : CardType.King;
                var otherCardFromAnnounce = new Card(context.FirstPlayedCard.Suit, otherTypeFromAnnounce);

                this.enemyCards.Add(otherCardFromAnnounce);
                this.allCards[otherCardFromAnnounce.Suit][otherCardFromAnnounce.Type] = CardStatus.InEnemy;
            }

            if (currentGameState == GameStates.FinalRoundState && context.CardsLeftInDeck == 0 && this.Cards.Count == 6)
            {
                this.enemyCards = this.ProvideEnemyCards(context);

                /*this.enemyCards = new HashSet<Card>(this.cardsLeft);
                if (context.FirstPlayedCard != null)
                {
                    this.enemyCards.Remove(context.FirstPlayedCard);
                }*/
            }

            if (this.PlayerActionValidator.IsValid(PlayerAction.ChangeTrump(), context, this.Cards))
            {
                return this.ChangeTrump(context.TrumpCard);
            }

            if (this.Cards.Count == 1)
            {
                return this.PlayCard(this.Cards.First());
            }

            ////TODO: Improve response
            if (context.FirstPlayedCard != null)
            {
                var enemyCard = context.FirstPlayedCard;
                var enemyCardPriority = this.cardChooser.GetCardPriority(enemyCard);
                var possibleCards = this.PlayerActionValidator.GetPossibleCardsToPlay(context, this.Cards);
                var trumpSuit = context.TrumpCard.Suit;
                // Use trump to take high card
                if (enemyCardPriority == 2 && enemyCard.Suit != trumpSuit && possibleCards.Any(c => c.Suit == trumpSuit))
                {
                    // TODO: Refactor this to achieve HQC
                    var trump =
                       possibleCards.Where(c => c.Suit == context.TrumpCard.Suit)
                            .OrderBy(this.cardChooser.GetCardPriority)
                            .FirstOrDefault();
                    return this.PlayCard(trump);
                }

                if (possibleCards.Any(c => c.Suit == enemyCard.Suit && c.GetValue() > enemyCard.GetValue()))
                {
                    // TODO: Refactor this to achieve HQC
                    var higherCard =
                        possibleCards.Where(c => c.Suit == enemyCard.Suit).OrderBy(c => c.GetValue()).LastOrDefault();

                    return this.PlayCard(higherCard);
                }
                
                var card = possibleCards.Where(c => c.Suit != trumpSuit).OrderBy(c => c.GetValue()).FirstOrDefault();
                //if (context.State.ShouldObserveRules)
                //{
                //    card = this.ChooseCardToPlay(this.allCards, context, possibleCards);
                //}

                //card = this.ChooseCardToPlay(this.allCards, context, possibleCards);

                //var card = this.PlayerActionValidator
                //    .GetPossibleCardsToPlay(context, this.Cards)
                //    .OrderBy(c => c.GetValue())
                //    .FirstOrDefault(c => c.Suit != context.TrumpCard.Suit);

                if (card == null)
                {
                    card = possibleCards.OrderBy(c => c.GetValue()).FirstOrDefault();
                }

                return this.PlayCard(card);
            }

            PlayerAction action = this.SelectBestCardWhenShouldPlayFirst(context);
            return action;
        }

        public override void EndTurn(PlayerTurnContext context)
        {
            if (this.Cards.Count <= 6)
            {
                this.enemyCards.Remove(context.FirstPlayedCard);
                this.enemyCards.Remove(context.SecondPlayedCard);
            }

            // If round ends with 20 or 40 announce one of the players could have not played a card
            if (context.FirstPlayedCard != null)
            {
                this.allCards[context.FirstPlayedCard.Suit][context.FirstPlayedCard.Type] = CardStatus.Passed;
            }

            if (context.SecondPlayedCard != null)
            {
                this.allCards[context.SecondPlayedCard.Suit][context.SecondPlayedCard.Type] = CardStatus.Passed;
            }

            base.EndTurn(context);
        }

        public override void StartRound(ICollection<Card> cards, Card trumpCard, int myTotalPoints, int opponentTotalPoints)
        {
            base.StartRound(cards, trumpCard, myTotalPoints, opponentTotalPoints);
            this.InitializeCards(this.allCards, this.Cards, this.cardSuits, this.cardTypes);
        }

        public override void AddCard(Card card)
        {
            this.allCards[card.Suit][card.Type] = CardStatus.InStalker;

            //// Something strange happens here. When program stops here the Stalker player still have 5 cards, but UI player already played a card on the context.
            base.AddCard(card);
        }

        public override void EndRound()
        {
            this.enemyCards.Clear();
            base.EndRound();
        }

        private PlayerAction SelectBestCardWhenShouldPlayFirst(PlayerTurnContext context)
        {
            var currentGameState = context.State.GetType().Name;
            var possibleCards = this.PlayerActionValidator.GetPossibleCardsToPlay(context, this.Cards);
            var cardToPlay = this.CheckForAnounce(context.TrumpCard.Suit, context.CardsLeftInDeck, currentGameState);

            if (cardToPlay != null)
            {
                return this.PlayCard(cardToPlay);
            }

            if (currentGameState == GameStates.StartRoundState)
            {
                var smallestCard = this.cardChooser.ChooseCardToPlay(context, possibleCards);

                return this.PlayCard(smallestCard);
            }
            else if (currentGameState == GameStates.FinalRoundState && context.CardsLeftInDeck == 0)
            {
                var cardsByPower = this.Cards.OrderByDescending(c => c.GetValue());
                foreach (var card in cardsByPower)
                {
                    if (this.EnemyContainsLowerCardThan(card) && !this.EnemyContainsGreaterCardThan(card))
                    {
                        return this.PlayCard(card);
                    }
                }

                bool enemyHasTrump = this.enemyCards.Any(c => c.Suit == context.TrumpCard.Suit);
                Card cardThatEnemyHasNotAsSuit = this.GetCardWithSuitThatEnemyHasNot(enemyHasTrump, context.TrumpCard.Suit);
                if (cardThatEnemyHasNotAsSuit == null)
                {
                    cardThatEnemyHasNotAsSuit = cardsByPower.LastOrDefault();
                    //cardThatEnemyHasNotAsSuit = this.ChooseCardToPlay(this.allCards, context, possibleCards);
                }

                return this.PlayCard(cardThatEnemyHasNotAsSuit);
            }
            else if (currentGameState == GameStates.FinalRoundState)
            {
                IEnumerable<Card> orderedByPower = this.Cards.OrderByDescending(c => c.GetValue());
                Card trump = orderedByPower.FirstOrDefault(c => c.Suit == context.TrumpCard.Suit);

                if (trump != null && !this.HasGreatherNonPassedCardThan(trump))
                {
                    return this.PlayCard(trump);
                }

                foreach (var card in orderedByPower)
                {
                    if (this.HasSmallerNonPassedCardThan(card))
                    {
                        return this.PlayCard(card);
                    }
                }

                cardToPlay = orderedByPower.Last();
            }
            else if (currentGameState == GameStates.TwoCardsLeftRoundState)
            {
                cardToPlay =
                    this.Cards.Where(c => c.Suit != context.TrumpCard.Suit).OrderBy(c => c.GetValue()).FirstOrDefault();
            }
            else if (currentGameState == GameStates.MoreThanTwoCardsLeftRoundState)
            {
                if (this.CanCloseTheGame(context) && context.State.CanClose)
                {
                    return this.CloseGame();
                }

                cardToPlay = this.cardChooser.ChooseCardToPlay(context, possibleCards);
            }

            return this.PlayCard(cardToPlay);
        }

        private bool CanCloseTheGame(PlayerTurnContext context)
        {
            //// When we have A and 10 from trumps; necessary points && some other winning cards
            //// In the current context we are first player.
            bool hasHighTrumps = this.allCards[context.TrumpCard.Suit][CardType.Ace] == CardStatus.InStalker &&
                                      this.allCards[context.TrumpCard.Suit][CardType.Ten] == CardStatus.InStalker;
            bool has40 = this.allCards[context.TrumpCard.Suit][CardType.King] == CardStatus.InStalker
                         && this.allCards[context.TrumpCard.Suit][CardType.Queen] == CardStatus.InStalker;

            bool hasEnoughAfterAnounce = context.FirstPlayerRoundPoints > 25;

            bool hasNecessaryPoints = this.Cards.Sum(c => c.GetValue()) + context.FirstPlayerRoundPoints > 70;

            int sureWiningCards = 0;
            foreach (var card in this.Cards)
            {
                if (!this.HasGreatherNonPassedCardThan(card))
                {
                    sureWiningCards++;
                }
            }

            if (has40 && hasEnoughAfterAnounce)
            {
                return true;
            }

            if (hasHighTrumps && hasNecessaryPoints && sureWiningCards > 0)
            {
                return true;
            }

            return false;
        }

        private Card GetCardWithSuitThatEnemyHasNot(bool enemyHasATrumpCard, CardSuit trumpSuit)
        {
            if (!enemyHasATrumpCard)
            {
                //// In case enemy does not have any trump cards and we have a trump, should throw a trump;
                IEnumerable<Card> myTrumpCards = this.Cards.Where(c => c.Suit == trumpSuit);
                if (myTrumpCards.Count() > 0)
                {
                    return myTrumpCards.OrderBy(c => c.GetValue()).LastOrDefault();
                }
            }

            var orderedCards = this.Cards.OrderBy(c => c.GetValue());
            foreach (var card in orderedCards)
            {
                if (this.enemyCards.All(c => c.Suit != card.Suit))
                {
                    if (enemyHasATrumpCard)
                    {
                        return this.Cards.Where(c => c.Suit == card.Suit).OrderBy(c => c.GetValue()).First();
                    }
                    else
                    {
                        return this.Cards.Where(c => c.Suit == card.Suit).OrderByDescending(c => c.GetValue()).First();
                    }
                }
            }

            return null;
        }

        private HashSet<Card> ProvideEnemyCards(PlayerTurnContext context)
        {
            HashSet<Card> result = new HashSet<Card>();

            for (int i = 0; i < this.cardSuits.Length; i++)
            {
                CardSuit suit = this.cardSuits[i];
                for (int j = 0; j < this.cardTypes.Length; j++)
                {
                    CardType type = this.cardTypes[j];
                    if (this.allCards[suit][type] == CardStatus.InDeckOrEnemy || this.allCards[suit][type] == CardStatus.InEnemy)
                    {
                        this.allCards[suit][type] = CardStatus.InEnemy;
                        result.Add(new Card(suit, type));
                    }
                }
            }

            if (context.FirstPlayedCard != null)
            {
                result.Remove(context.FirstPlayedCard);
            }

            return result;
        }

        // TODO: Refactor in one method
        private bool EnemyContainsLowerCardThan(Card card)
        {
            return this.allCards[card.Suit].Any(c => c.Value == CardStatus.InEnemy && new Card(card.Suit, c.Key).GetValue() < card.GetValue());
        }

        private bool EnemyContainsGreaterCardThan(Card card)
        {
            return this.allCards[card.Suit].Any(c => c.Value == CardStatus.InEnemy && new Card(card.Suit, c.Key).GetValue() > card.GetValue());
        }

        private bool HasGreatherNonPassedCardThan(Card card)
        {
            return this.allCards[card.Suit].Any(c => c.Value == CardStatus.InDeckOrEnemy && new Card(card.Suit, c.Key).GetValue() > card.GetValue());
        }

        private bool HasSmallerNonPassedCardThan(Card card)
        {
            return this.allCards[card.Suit].Any(c => c.Value == CardStatus.InDeckOrEnemy && new Card(card.Suit, c.Key).GetValue() < card.GetValue());
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
                    CardType otherTypeForAnnounce = card.Type == CardType.King ? CardType.Queen : CardType.King;
                    var otherCardForAnnounce = new Card(card.Suit, otherTypeForAnnounce);

                    //// instead to search this.Cards.Conatains(otherCardForAnounce);
                    if (this.allCards[card.Suit][otherTypeForAnnounce] == CardStatus.InStalker)
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
                CardStatus cardStatusForTen = this.allCards[trumpSuit][CardType.Ten];
                CardStatus cardStatusForAce = this.allCards[trumpSuit][CardType.Ace];

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
                Card cardToReturn = new Card(announcePairs[0].Suit, announcePairs[0].Type);

                //// They will be ordered in this way: [Q♦ K♦; K♠ Q♠; К♣ Q♣] by pairs: two diamonds, two clubs e.t.c. so incrementation will be i+=2
                for (int i = 0; i < announcePairs.Count; i += 2)
                {
                    CardSuit currentSuit = announcePairs[i].Suit;
                    CardStatus cardStatusForTen = this.allCards[currentSuit][CardType.Ten];
                    CardStatus cardStatusForAce = this.allCards[currentSuit][CardType.Ace];

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

        private void InitializeCards(IDictionary<CardSuit, Dictionary<CardType, CardStatus>> allCardsToInitialize, ICollection<Card> cards, CardSuit[] allCardSuits, CardType[] allCardTypes)
        {
            foreach (Card card in cards)
            {
                allCardsToInitialize[card.Suit][card.Type] = CardStatus.InStalker;
            }

            var result = new List<Card>();
            // CardSuit[] cardSuits = new[] { CardSuit.Club, CardSuit.Diamond, CardSuit.Heart, CardSuit.Spade };
            // CardType[] cardTypes = new[] { CardType.Ace, CardType.Ten, CardType.King, CardType.Queen, CardType.Jack, CardType.Nine };
            for (int i = 0; i < this.cardSuits.Length; i++)
            {
                for (int j = 0; j < allCardTypes.Length; j++)
                {
                    if (!this.Cards.Contains(new Card(allCardSuits[i], allCardTypes[j])))
                    {
                        result.Add(new Card(allCardSuits[i], allCardTypes[j]));

                        if (!this.allCards[allCardSuits[i]].ContainsKey(allCardTypes[j]))
                        {
                            this.allCards[allCardSuits[i]].Add(allCardTypes[j], CardStatus.InDeckOrEnemy);
                        }
                        else
                        {
                            this.allCards[allCardSuits[i]][allCardTypes[j]] = CardStatus.InDeckOrEnemy;
                        }
                    }
                    else
                    {
                        if (!this.allCards[allCardSuits[i]].ContainsKey(allCardTypes[j]))
                        {
                            this.allCards[allCardSuits[i]].Add(allCardTypes[j], CardStatus.InStalker);
                        }
                        else
                        {
                            this.allCards[allCardSuits[i]][allCardTypes[j]] = CardStatus.InStalker;
                        }
                    }
                }
            }
        }
    }
}
