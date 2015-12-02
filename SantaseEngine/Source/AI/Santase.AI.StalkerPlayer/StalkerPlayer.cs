namespace Santase.AI.StalkerPlayer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Logic;
    using Logic.Cards;
    using Logic.Extensions;
    using Logic.Players;

    public class StalkerPlayer : BasePlayer
    {
        private readonly CardSuit[] cardSuits = new[] { CardSuit.Club, CardSuit.Diamond, CardSuit.Heart, CardSuit.Spade };
        private readonly CardType[] cardTypes = new[] { CardType.Ace, CardType.Ten, CardType.King, CardType.Queen, CardType.Jack, CardType.Nine };
        private HashSet<Card> enemyCards = new HashSet<Card>();
        private Dictionary<CardSuit, Dictionary<CardType, CardStatus>> allCards;
        private EnemyPlayerStatistics enemyStats;

        public StalkerPlayer()
        {
            this.enemyStats = new EnemyPlayerStatistics();

            this.allCards = new Dictionary<CardSuit, Dictionary<CardType, CardStatus>>();
            this.allCards.Add(CardSuit.Club, new Dictionary<CardType, CardStatus>());
            this.allCards.Add(CardSuit.Diamond, new Dictionary<CardType, CardStatus>());
            this.allCards.Add(CardSuit.Heart, new Dictionary<CardType, CardStatus>());
            this.allCards.Add(CardSuit.Spade, new Dictionary<CardType, CardStatus>());
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
                var enemyCardPriority = this.GetCardPriority(enemyCard);
                var possibleCards = this.PlayerActionValidator.GetPossibleCardsToPlay(context, this.Cards);
                var trumpSuit = context.TrumpCard.Suit;
                // Use trump to take high card
                if (enemyCardPriority == 2 && enemyCard.Suit != trumpSuit && possibleCards.Any(c => c.Suit == trumpSuit))
                {
                    // TODO: Refactor this to achieve HQC
                    var trump =
                       possibleCards.Where(c => c.Suit == context.TrumpCard.Suit)
                            .OrderBy(this.GetCardPriority)
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

                Card card;
                card = possibleCards.Where(c => c.Suit != trumpSuit).OrderBy(c => c.GetValue()).FirstOrDefault();
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
                var smallestCard = this.ChooseCardToPlay(this.allCards, context, possibleCards);

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

                //IEnumerable<Card> nonTrumpCards = this.Cards.Where(c => c.Suit != context.TrumpCard.Suit).OrderBy(c => c.GetValue());
                //foreach (var card in nonTrumpCards)
                //{
                //    switch (card.Type)
                //    {
                //        case CardType.Nine:
                //            return this.PlayCard(card);

                //        case CardType.Jack:
                //            return this.PlayCard(card);

                //        case CardType.Queen:
                //            {
                //                if (!this.IsCardWaitingForAnnounce(card))
                //                {
                //                    return this.PlayCard(card);
                //                }

                //                break;
                //            }

                //        case CardType.King:
                //            {
                //                if (!this.IsCardWaitingForAnnounce(card))
                //                {
                //                    return this.PlayCard(card);
                //                }

                //                break;
                //            }
                //    }
                //}

                //cardToPlay = nonTrumpCards.FirstOrDefault();

                cardToPlay = this.ChooseCardToPlay(this.allCards, context, possibleCards);
            }

            /*if (cardToPlay == null)
            {
                cardToPlay = possibleCardsToPlay.First(c => c.Suit != context.TrumpCard.Suit);
            }*/

            return this.PlayCard(cardToPlay);
        }

        private int GetCardPriority(Card card)
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
            CardStatus statusOfOtherCard = this.allCards[card.Suit][otherTypeForAnnounce];
            if (statusOfOtherCard == CardStatus.InDeckOrEnemy)
            {
                return true;
            }

            return false;
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

        public int GetSuitPriority(
            IDictionary<CardSuit, Dictionary<CardType, CardStatus>> allCardsToCheck,
            CardSuit cardSuit)
        {
            return allCardsToCheck[cardSuit].Count(card => card.Value == CardStatus.Passed || card.Value == CardStatus.InStalker);
        }

        public int GetTrumpPriority(
            IDictionary<CardSuit, Dictionary<CardType, CardStatus>> allCardsToCheck, CardSuit trumpSuit, PlayerTurnContext context)
        {
            var countOfTrump = this.GetSuitPriority(allCardsToCheck, trumpSuit);
            if (context.CardsLeftInDeck != 0)
            {
                countOfTrump++;
            }

            return countOfTrump;
        }

        public int[] GetPriorityForEachSuit(
            IDictionary<CardSuit, Dictionary<CardType, CardStatus>> allCardsToCheck, PlayerTurnContext context)
        {
            var prioritiesPerSuit = new int[4];
            var trumpSuit = context.TrumpCard.Suit;
            var trumpPriority = this.GetTrumpPriority(allCardsToCheck, trumpSuit, context);

            if (context.State.ShouldObserveRules)
            {
                for (int i = 0; i < 4; i++)
                {
                    if ((int)trumpSuit != i)
                    {
                        prioritiesPerSuit[i] = this.GetSuitPriority(allCardsToCheck, (CardSuit)i) - trumpPriority;
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

        public Card ChooseCardToPlay(
            IDictionary<CardSuit, Dictionary<CardType, CardStatus>> allCardsToCheck,
            PlayerTurnContext context,
            ICollection<Card> stalkerCards)
        {
            var trumpSuit = context.TrumpCard.Suit;
            int highestPrioritySuit = 0;
            int priorityValue = Int32.MaxValue;

            // Get priorities for all suits
            var suitsPriorities = this.GetPriorityForEachSuit(allCardsToCheck, context);

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
    }
}
