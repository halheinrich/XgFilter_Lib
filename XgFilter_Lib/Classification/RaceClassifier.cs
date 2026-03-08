namespace XgFilter_Lib.Classification;

public sealed class RaceClassifier : IPositionClassifier
{
    public bool Matches(int[] board)
    {
        int playerLast = -1;
        int opponentFirst = 26;

        for (int i = 0; i < 26; i++)
        {
            if (board[i] > 0) playerLast = i;
            if (board[i] < 0 && i < opponentFirst) opponentFirst = i;
        }

        if (playerLast == -1 || opponentFirst == 26) return true;
        return playerLast < opponentFirst;
    }
}