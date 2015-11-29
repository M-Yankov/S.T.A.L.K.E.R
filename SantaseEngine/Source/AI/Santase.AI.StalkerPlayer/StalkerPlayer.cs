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
        //private readonly IList<Card> passedCards = new List<Card>(24);
        private readonly CardSuit[] cardSuits = new[] { CardSuit.Club, CardSuit.Diamond, CardSuit.Heart, CardSuit.Spade };
        private readonly CardType[] cardTypes = new[] { CardType.Ace, CardType.Ten, CardType.King, CardType.Queen, CardType.Jack, CardType.Nine };
        private HashSet<Card> enemyCards = new HashSet<Card>();
        private IList<Card> cardsLeft = new List<Card>(24);
        private Dictionary<CardSuit, Dictionary<CardType, CardStatus>> allCards;
        private Card lastPlayerCardFromUs;
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
            string currentGameState = context.State.GetType().Name;

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

            //// ToDo fix response
            if (context.FirstPlayedCard != null)
            {
                var card = this.PlayerActionValidator
                    .GetPossibleCardsToPlay(context, this.Cards)
                    .OrderBy(c => c.GetValue())
                    .FirstOrDefault(c => c.Suit != context.TrumpCard.Suit);

                if (card == null)
                {
                    card = this.PlayerActionValidator.GetPossibleCardsToPlay(context, this.Cards).FirstOrDefault();
                }

                return this.PlayCard(card);
            }

            PlayerAction action = this.SelectBestCardWhenShouldPlayFirst(context);
            return action;
        }

        public override void EndTurn(PlayerTurnContext context)
        {
            // this.cardsLeft.Remove(context.FirstPlayedCard);
            // this.cardsLeft.Remove(context.SecondPlayedCard);

            // this.passedCards.Add(context.FirstPlayedCard);
            // this.passedCards.Add(context.SecondPlayedCard);0

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
            //this.passedCards.Clear();
            this.enemyCards.Clear();
            base.EndRound();
        }

        private PlayerAction SelectBestCardWhenShouldPlayFirst(PlayerTurnContext context)
        {
            string currentGameState = context.State.GetType().Name;

            Card cardToPlay = this.CheckForAnounce(context.TrumpCard.Suit, context.CardsLeftInDeck, currentGameState);

            if (cardToPlay != null)
            {
                return this.PlayCard(cardToPlay);
            }

            if (currentGameState == GameStates.StartRoundState)
            {
                //// possible null value;
                Card smallestCard = this.Cards
                    .Where(c => c.Suit != context.TrumpCard.Suit && c.Type != CardType.King && c.Type != CardType.Queen)
                    .OrderBy(c => c.GetValue())
                    .FirstOrDefault();

                if (smallestCard == null)
                {
                    smallestCard =
                        this.Cards.Where(c => c.Suit != context.TrumpCard.Suit)
                            .OrderBy(c => c.GetValue())
                            .FirstOrDefault();
                }

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
                //// get rid off card that has one member of its suit < 10; examples [9♦ J♦ J♣ Q♠ K♠ A♠] -> J♣, [10♦ 10♠ 9♦ K♦ J♣ Q♣] -> 9♦
                var groupedCards = this.Cards.GroupBy(c => c.Suit).OrderBy(g => g.Count());

                foreach (var group in groupedCards)
                {
                    if (group.Key == context.TrumpCard.Suit)
                    {
                        continue;
                    }

                    foreach (var card in group)
                    {
                        if (card.GetValue() < 10)
                        {
                            return this.PlayCard(card);
                        }
                    }
                }

                //// if our cards are something like [10♥ 10♣ 10♠ A♠ 10♦ A♦], :D just throw first non-trump; // it will be some of tens
                //// or trump suit is ♦ and cards are[9♦ J♦ K♦ 10♦ 10♣ A♥] -> 9♦ is it good enough?
                cardToPlay = groupedCards.Last(g => g.Key != context.TrumpCard.Suit).First();
            }
            else if (currentGameState == GameStates.MoreThanTwoCardsLeftRoundState)
            {
                if (this.CanCloseTheGame(context) && context.State.CanClose)
                {
                    return this.CloseGame();
                }

                IEnumerable<Card> nonTrumpCards = this.Cards.Where(c => c.Suit != context.TrumpCard.Suit).OrderBy(c => c.GetValue());
                foreach (var card in nonTrumpCards)
                {
                    switch (card.Type)
                    {
                        case CardType.Nine:
                            return this.PlayCard(card);

                        case CardType.Jack:
                            return this.PlayCard(card);

                        // case CardType.Ten:
                        //    {
                        //        if (this.allCards[card.Suit][CardType.Ace] == CardStatus.Passed ||
                        //         this.allCards[card.Suit][CardType.Ace] == CardStatus.InStalker)
                        //        {
                        //            return this.PlayCard(card);
                        //        }

                        // break;
                        // }
                        case CardType.Queen:
                            {
                                if (!this.IsCardWaitsForAnnounce(card))
                                {
                                    return this.PlayCard(card);
                                }

                                break;
                            }

                        case CardType.King:
                            {
                                if (!this.IsCardWaitsForAnnounce(card))
                                {
                                    return this.PlayCard(card);
                                }

                                break;
                            }
                    }
                }

                cardToPlay = nonTrumpCards.First();
            }

            /*if (cardToPlay == null)
            {
                cardToPlay = possibleCardsToPlay.First(c => c.Suit != context.TrumpCard.Suit);
            }*/

            return this.PlayCard(cardToPlay);
        }

        private bool IsCardWaitsForAnnounce(Card card)
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
            bool hasNecessaryTrumps = this.allCards[context.TrumpCard.Suit][CardType.Ten] == CardStatus.InStalker &&
                                      this.allCards[context.TrumpCard.Suit][CardType.Ten] == CardStatus.InStalker;
            bool hasNecessaryPoints = this.Cards.Sum(c => c.GetValue()) + context.FirstPlayerRoundPoints > 60;

            int sureWiningCards = 0;
            foreach (var card in this.Cards)
            {
                if (!this.HasGreatherNonPassedCardThan(card))
                {
                    sureWiningCards++;
                }
            }

            if (hasNecessaryTrumps && hasNecessaryPoints && sureWiningCards > 1)
            {
                return true;
            }

            return false;
        }

        private Card GetCardWithSuitThatEnemyHasNot(bool enemyHasATrumpCard, CardSuit trumpSuit)
        {
            if (!enemyHasATrumpCard)
            {
                //// if enemy has not trumps and we have a trump, should throw a trump;
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

                    //// Return bigger if 10 and A of current Suit is passed or is in us. But it will suspicious for enemy if we throw a King.
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
