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
            if (currentGameState == GameStates.StartRoundState)
            {
                this.cardsLeft = this.InitizlizeCardsLeft();
            }

            if (context.FirstPlayerAnnounce != Announce.None)
            {
                //// If enemy has announce, add other card from announce to enemy card collection.
                CardType otherTypeFromAnnounce = context.FirstPlayedCard.Type == CardType.King ? CardType.Queen : CardType.King;
                var otherCardFromAnnounce = new Card(context.FirstPlayedCard.Suit, otherTypeFromAnnounce);

                this.enemyCards.Add(otherCardFromAnnounce);
                this.allCards[otherCardFromAnnounce.Suit][otherCardFromAnnounce.Type] = CardStatus.InEnemy;
            }

            //// Optimize if necessary.
            foreach (Card card in this.Cards)
            {
                this.allCards[card.Suit][card.Type] = CardStatus.InStalker;

                if (this.cardsLeft.Contains(card))
                {
                    this.cardsLeft.Remove(card);
                }
            }

            if (currentGameState == GameStates.FinalRoundState)
            {
                this.enemyCards = new HashSet<Card>(this.cardsLeft);
                if (context.FirstPlayedCard != null)
                {
                    this.enemyCards.Remove(context.FirstPlayedCard);
                }
            }

            if (this.PlayerActionValidator.IsValid(PlayerAction.ChangeTrump(), context, this.Cards))
            {
                return this.ChangeTrump(context.TrumpCard);
            }

            //// TOdo When To close the game

            //// ToDo if we are on turn must play first, else respond the first played card.
            return this.SelectBestCard(context);
        }

        public override void EndTurn(PlayerTurnContext context)
        {
            this.cardsLeft.Remove(context.FirstPlayedCard);
            this.cardsLeft.Remove(context.SecondPlayedCard);

            this.passedCards.Add(context.FirstPlayedCard);
            this.passedCards.Add(context.SecondPlayedCard);

            this.allCards[context.FirstPlayedCard.Suit][context.FirstPlayedCard.Type] = CardStatus.Passed;
            this.allCards[context.SecondPlayedCard.Suit][context.SecondPlayedCard.Type] = CardStatus.Passed;

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
            string currentGameState = context.State.GetType().Name;

            Card cardToPlay = this.CheckForAnonuce(context.TrumpCard.Suit, context.CardsLeftInDeck, currentGameState);

            if (cardToPlay != null)
            {
                return this.PlayCard(cardToPlay);
            }

            if (currentGameState == GameStates.StartRoundState)
            {
                Card smallestCard = this.Cards.Where(c => c.Suit != context.TrumpCard.Suit).OrderByDescending(c => c.GetValue()).First();
                return this.PlayCard(smallestCard);
            }
            else if (currentGameState == GameStates.FinalRoundState && context.CardsLeftInDeck == 0)
            {
                //// TODO:
                //// if Cards in deck == 0; 100% know enemy cards

            }
            else if (currentGameState == GameStates.TwoCardsLeftRoundState)
            {
                //// TODO:
                //// the status here is that we play first. So check is a big deal to take the trump card and if TRUE play a small lonely card from cardType
                //// make groups then from group that has small count play smallest only if not a 10 or A;
            }

            this.lastPlayerCardFromUs = cardToPlay;
            return this.PlayCard(cardToPlay);
        }

        private Card CheckForAnonuce(CardSuit trumpSuit, int cardsLeftInDeck, string isFirstState)
        {
            if (isFirstState == GameStates.StartRoundState)
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

                        if (!this.allCards[cardSuits[i]].ContainsKey(cardTypes[j]))
                        {
                            this.allCards[cardSuits[i]].Add(cardTypes[j], CardStatus.InDeckOrEnemy);
                        }
                        else
                        {
                            this.allCards[cardSuits[i]][cardTypes[j]] = CardStatus.InDeckOrEnemy;
                        }
                    }
                    else
                    {
                        if (!this.allCards[cardSuits[i]].ContainsKey(cardTypes[j]))
                        {
                            this.allCards[cardSuits[i]].Add(cardTypes[j], CardStatus.InStalker);
                        }
                        else
                        {
                            this.allCards[cardSuits[i]][cardTypes[j]] = CardStatus.InStalker;
                        }
                    }
                }
            }

            return result;
        }
    }
}
