# S.T.A.L.K.E.R.

###  ♠♣♦♥ Teamwork documentation. ♥♦♣♠

 1. Initialization:<br />
    First of all, get the current state.
    * In first state initialize the `cadrs storage`
      where on each card assign a `CardStatus` depends on the location of the card
    (in S.T.A.L.K.E.R, enemyOrDeck, enemy or passed). On each turn using the `Add()`
    method from `BasePlayer` cardStatus of the newly added card is updated - in S.T.A.L.K.E.R.
    Then using the `EndTurn()` for updating card status to passed. Both with constant complexity.
    Initialization is implemented using `StartRound()`
    * In final state, when no cards left in deck initialize enemy cards. We already knows it.
     `EnemyCards = AllCards - stalkerCards - passed cards.` So it will be easy to make decision when no cards in deck.
    [code](https://github.com/M-Yankov/S.T.A.L.K.E.R/blob/master/SantaseEngine/Source/AI/Santase.AI.StalkerPlayer/CardHelpers/CardHolder.cs#L42)
 1. Decision when should play first
    * Depends on the game state
        - StartRoundState : <br />
        _**PriorityLogic**_ [code](https://github.com/M-Yankov/S.T.A.L.K.E.R/blob/master/SantaseEngine/Source/AI/Santase.AI.StalkerPlayer/CardHelpers/CardChooser.cs)
        - MoreThanTwoCardsLeftRoundState : <br />
        _**PriorityLogic**_ [code](https://github.com/M-Yankov/S.T.A.L.K.E.R/blob/master/SantaseEngine/Source/AI/Santase.AI.StalkerPlayer/CardHelpers/CardChooser.cs)
        - TwoCardsLeftRoundState: <br />
        Minimal card != trump suit.
        - FinalRoundState: <br />
        Already knows the cards of enemy so checks, if the stalker player can take some of the enemy's card and
        then play it. If there are no card to take, then play a card with suit that enemy has not. It can be
        from trump suit (it's sure points) otherwise just play small card.
 2. Decision when must response
    * First apply logic when card is high (`A` or `10`) and then try to take it with trump. (Smaller or bigger trump -> _**see last updates**_)
    * Just get the card with most bigger from this suit.
    * If nothing from above applies then throw first smaller non trump card.
 1. Exchanging trump card is implemented by using `PlayerActionValidator`
    class inherited  by `BasePlayer` parent class.
 is `A` or `10` get with more bigger/smaller trump _(**See last updates)**_ . Of course if the bot has any trump.
 1. Logic to close game
    - ...
 1. Logic for announce
    - Checks for pairs of `K` and `Q` **(from/with)** same suit. If the trump suit is `♦` and the cards of
    stalker player are: `[K♠ Q♠ K♦ Q♦ K♣ Q♣]` should announce 40. Else check from pairs first suit that
    `A` and `10` are passed and throw `K`, else just the `Q`. _This logic is checked before every other option
    because it gains 20/40 points just with plaing_ . [code](https://github.com/M-Yankov/S.T.A.L.K.E.R/blob/master/SantaseEngine/Source/AI/Santase.AI.StalkerPlayer/CardHelpers/StalkerHelper.cs#L94)
 1. _Priority_ logic:
    - ...
 1. Cards storage -
    
    ```CSharp
    CardSuit[] allCardSuit;
    CardType[] allCardTypes;
    IList<Card> enemyCards;
    IDictionary<CardSuit, Dictionary<CardType, CardStatus>> allCards;
    ```
    
    The constant access to object is the fastest way get it. Example:
    
    ```C#
    this.allCards[CardSuit.Club][CardType.Ace] // gets or sets card status;
    ```
    
 2. Additional logic when enemy has an announce gets other card from announce. Example:
    ```
    Enemy announces 20 with `K♣`, the stalker player must know about other card `Q♣` that is in enemy.
    ```
 1. Pull request for bug fixing [#12](https://github.com/NikolayIT/SantaseGameEngine/pull/12/files)
