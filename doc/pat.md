|n  |example        |info               |
|---|---            |---                |
|1  |10             |begin              |
|5  |6801062301     |                   |
|2  |02             |mode               |
|2  |001a           |CPU Power          |
|1  |00             |                   |
|1  |2c/38/3c       |CPU Temp           |
|1  |0007           |CPU Usage          |
|2  |09c4           |CPU Freq           |
|2  |0393           |Fan Freq           |
|2  |0015           |GPU Power          |
|4  |44/40/70       |GPU Temp           |
|1  |0014           |GPU Usage          |
|2  |00ff           |GPU Freq           |
|12 |...            |PSU stuff          |
|1  |aa             |checksum           |
|1  |16             |end                |
|22 |00...          |                   |


|d  |Mode           |
|00 |off            |
|01 |full light     |
|02 |CPU Freq       |
|03 |CPU Fan        |
|04 |GPU Freq       |
|05 |PSU 1          |
|06 |PSU 2          |


|DATA       |seven-segment display count|
|---        |---                        |
|CPU temp   |3                          |
|POWER      |4                          |
|CPU Usage  |3                          |
|FAN/FREQ   |4                          |
