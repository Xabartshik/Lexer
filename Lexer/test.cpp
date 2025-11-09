#include <iostream>
#include <fstream>
#include <vector>
using namespace std;



int main() {
    int m, n, eaten;
    cin >> m;
    cin >> n;

    vector<vector<int>> grid(m, vector<int>(n, 1));
    for (int i = 0; i < m; i++) 
    {
        cin >> eaten;
        for (int j = 0; j < eaten; j++)
        {
            grid[i][j] = 0;
        }

    }

    // Создание таблицы динамического программирования
    vector<vector<bool>> dp(m, vector<bool>(n, false));

    // Инициализация последней строки
    for (int j = 0; j < n; j++) {
        if (grid[m - 1][j] == 0) {
            dp[m - 1][j] = true;
        }
    }

    // Заполнение таблицы
    for (int i = m - 2; i >= 0; i--) {
        for (int j = 0; j < n; j++) {
            if (grid[i][j] == 0) {
                // Если текущая клетка не отравлена, то игрок может выиграть, если:
                // - он съест клетку справа (если она не отравлена) и выиграет в подпроблеме справа
                // - он съест клетку снизу (если она не отравлена) и выиграет в подпроблеме снизу
                dp[i][j] = (j + 1 < n && grid[i][j + 1] == 0 && dp[i][j + 1]) ||
                    (i + 1 < m && grid[i + 1][j] == 0 && dp[i + 1][j]);
            }
        }
    }

    // Вывод количества выигрышных ходов
    int count = 0;
    for (int i = 0; i < m; i++) {
        for (int j = 0; j < n; j++) {
            if (dp[i][j]) {
                count++;
            }
        }
    }
    cout << count << endl;

    // Вывод выигрышных ходов
    for (int i = 0; i < m; i++) {
        for (int j = 0; j < n; j++) {
            if (dp[i][j]) {
                cout << i + 1 << " " << j + 1 << endl;
            }
        }
    }

    return 0;
}