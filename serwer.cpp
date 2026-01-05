#include <cstdio>
#include <netdb.h>
#include <signal.h>
#include <sys/socket.h>
#include <unistd.h>
#include <unordered_set>
#include <vector>
#include <sys/epoll.h>
#include <cmath>
#include <cstdlib>
#include <cstring>
#include <errno.h>
#include <string>
#include <map>
#include <chrono>
#include <iostream>
#include <fcntl.h>

int ticks = 32;
int tcpPort;
int udpPort;
int nextPlayerID = 0;
int nextBulletID = 0;

int tcpFd;
int udpFd;  
int epollDesc;

class PlayerData {      
  public:
    int playerId;
    int tcpFd;
    int udpFd;
    std::string name;

    bool isAlive;
    int health;

    int positionX;
    int positionY;
    int rotation;
    int type;
    int damage;
    int bulletSpeed;
    int points;

    sockaddr_in playerAddr;
    std::string inputBuffer;
};

class BulletData {
  public:
    int bulletId;
    int ownerPlayerId;

    int positionX;
    int positionY;
    int direction;
    int speed;
    int lifetime;
    int damage;
};


std::map<int, PlayerData> players;
std::map<int, BulletData> bullets;

template <typename... Args> /* This mimics GNU 'error' function */
void error(int status, int errnum, const char *format, Args... args) {
    fprintf(stderr, (format + std::string(errnum ? ": %s\n" : "\n")).c_str(), args..., strerror(errnum));
    if (status) exit(status);
}

void setNonBlocking(int sock) {
    int flags = fcntl(sock, F_GETFL, 0);
    fcntl(sock, F_SETFL, flags | O_NONBLOCK);
}

// creates a socket listening on port specified in argv[1]
// int getaddrinfo_socket_bind_listen(int argc, const char *const *argv);
int getaddrinfo_socket_bind_listen(int sockType, const char *argv);

// handles SIGINT
void ctrl_c(int);

// sends data to clientFds excluding fd
void sendToAllBut(int fd, const char *buffer, int count);

void tcpSendMessageToPlayers(std::string buffer);

int getRandomNumberInRange(int min, int max){
    int randomNum = min + rand() % (max - min);
    return randomNum; 
}

void handleNewClient(epoll_event ee){
    // prepare placeholders for client address
    sockaddr_storage clientAddr{};
    socklen_t clientAddrSize = sizeof(clientAddr);

    // accept new connection
    auto clientFd = accept(tcpFd, (sockaddr *)&clientAddr, &clientAddrSize);
    if (clientFd == -1)
        perror("accept failed");

    setNonBlocking(clientFd);
    int flag = 1;
    setsockopt(clientFd, IPPROTO_TCP, O_NDELAY, (char *)&flag, sizeof(int));
    // add client to all clients set
    int playerId = nextPlayerID++;

    PlayerData playerData;
    playerData.playerId = playerId;
    playerData.tcpFd = clientFd;
    playerData.playerAddr.sin_addr = ((sockaddr_in *)&clientAddr)->sin_addr;
    playerData.playerAddr.sin_family = AF_INET;
    playerData.isAlive = false;
    playerData.health = 0;
    playerData.rotation = 0;
    players[playerId] = playerData;

    // tell who has connected
    char host[NI_MAXHOST], port[NI_MAXSERV];
    getnameinfo((sockaddr *)&clientAddr, clientAddrSize, host, NI_MAXHOST, port, NI_MAXSERV, 0);
    printf("New connection from: %s:%s (fd: %d)\n", host, port, clientFd);

    ee.data.fd = clientFd;
    epoll_ctl(epollDesc, EPOLL_CTL_ADD, clientFd, &ee);

    std::string s = std::to_string(udpPort) + ";" + std::to_string(playerId) + "~";
    send(clientFd, s.c_str(), s.size(), 0);
}

void handlePlayerDisconnect(int clientFd){
    int disconnectedPlayerId = -1;
    for (const auto& i : players) {
        if (i.second.tcpFd == clientFd) {
            disconnectedPlayerId = i.second.playerId;
            break;
        }
    }

    if(disconnectedPlayerId != -1){
        printf("Client Id: %d, tcpFd: %d disconnected\n", disconnectedPlayerId, clientFd);
        std::string discMessage = "D;" + std::to_string(disconnectedPlayerId) + "~";
        players.erase(disconnectedPlayerId);

        tcpSendMessageToPlayers(discMessage);
    }

    epoll_ctl(epollDesc, EPOLL_CTL_DEL, clientFd, nullptr);
    shutdown(clientFd, SHUT_RDWR);
    close(clientFd);
}

void tpcHandleMessageFromClient(epoll_event ee){

    int playerId = -1;
    int clientFd = ee.data.fd;

    for (auto& i : players) {
        if (i.second.tcpFd == clientFd) {
            playerId = i.second.playerId;
            break;
        }
    }

    if(playerId == -1){
        handlePlayerDisconnect(clientFd);
        return;
    }

    char buffer[255];
    int count = read(clientFd, buffer, 255);

    if (count < 1) {
        if (count == -1 && (errno == EAGAIN || errno == EWOULDBLOCK)) {
            return;
        }
        handlePlayerDisconnect(clientFd);
        return;
    }
    // printf("%s\n", buffer);
    players[playerId].inputBuffer += std::string(buffer, count);

    size_t pos = 0;
    while ((pos = players[playerId].inputBuffer.find('~')) != std::string::npos)
    {
        std::string message = players[playerId].inputBuffer.substr(0, pos);
        players[playerId].inputBuffer.erase(0, pos + 1);

        std::vector<char> msgBuf(message.begin(), message.end());
        msgBuf.push_back('\0');

        const char* del = ";";
        char *t = strtok(msgBuf.data(), del);
        
        if (!t) 
            continue;

        char messageType = t[0];

        switch (messageType){
            case 'I':{
                t = strtok(nullptr, del);
                int playerUdpPort = atoi(t);
                t = strtok(nullptr, del);
                std::string name = std::string(t);
                t = strtok(nullptr, del);
                int type = atoi(t);
                t = strtok(nullptr, del);
                int damage = atoi(t);
                t = strtok(nullptr, del);
                int bulletSpeed = atoi(t);
                t = strtok(nullptr, del);
                int hp = atoi(t);
                
                players[playerId].playerAddr.sin_port = htons(playerUdpPort);
                players[playerId].name = name;
                players[playerId].type = type;
                players[playerId].points = 0;
                players[playerId].isAlive = true;
                players[playerId].damage = damage;
                players[playerId].bulletSpeed = bulletSpeed;
                players[playerId].health = hp;
                players[playerId].positionX = getRandomNumberInRange(0, 1000);
                players[playerId].positionY = getRandomNumberInRange(0, 1000);


                std::string s = "I;" + std::to_string(players[playerId].positionX) + ";" + std::to_string(players[playerId].positionY) + "~";
                send(clientFd, s.c_str(), s.size(), 0);

                std::string map = "M;";
                for(const auto& i : players){
                    if(!i.second.isAlive)
                        continue;
                    map = map + std::to_string(i.second.playerId) + ";" + i.second.name + ";" + std::to_string(i.second.type) + ";" + std::to_string(i.second.positionX) + ";" + std::to_string(i.second.positionY) + ";" + std::to_string(i.second.rotation) + "!";
                }
                map += "~";
                int count = map.size();

                send(clientFd, map.c_str(), count, 0);

                std::string connMessage = "C;" + std::to_string(playerId) + ";" + players[playerId].name + ";" + std::to_string(players[playerId].type) + ";" + std::to_string(players[playerId].positionX) + ";" + std::to_string(players[playerId].positionY) + ";" + std::to_string(players[playerId].rotation) + "~";
                tcpSendMessageToPlayers(connMessage);

                break;
            }
            case 'S':
            {   
                std::cout << "Player " << playerId << " shooting" << std::endl;
                t = strtok(nullptr, del);
                int positionX = atoi(t);
                t = strtok(nullptr, del);
                int positionY = atoi(t);
                t = strtok(nullptr, del);
                int rotation = atoi(t);

                if(!players[playerId].isAlive){
                    std::cout << "Player " << playerId << " is dead, cannot shoot" << std::endl;
                    break;
                }

                BulletData bulletData;
                bulletData.bulletId = nextBulletID++;
                bulletData.ownerPlayerId = playerId;
                bulletData.positionX = positionX;
                bulletData.positionY = positionY;
                bulletData.direction = rotation;
                bulletData.speed = players[playerId].bulletSpeed;
                bulletData.lifetime = 128;
                bulletData.damage = players[playerId].damage;

                bullets[bulletData.bulletId] = bulletData;
                std::cout << "Player " << playerId << " shooted" << std::endl;
                //info o nowym pocisku do wszystkich
                std::string shootMessage = "S;" + std::to_string(bulletData.bulletId) + ";" + std::to_string(bulletData.ownerPlayerId) + ";" + std::to_string(bulletData.positionX) + ";" + std::to_string(bulletData.positionY) + ";" + std::to_string(bulletData.direction) + "~";
                tcpSendMessageToPlayers(shootMessage);
                break;
            }
            case 'C':
            {
                t = strtok(nullptr, del);
                int id = atoi(t);
                t = strtok(nullptr, del);
                std::string text = std::to_string(id) + std::string(t); // check if t is null first for safety

                if(clientFd != STDOUT_FILENO)
                    write(STDOUT_FILENO, buffer, count);
                sendToAllBut(clientFd, buffer, count);
                break;
            }
                
            default:{
                break;
            }
        }
    }
}

void udpHandleMessageFromClient(char* buffer){
    // printf("Received UDP message: %s\n", buffer);
    const char *del = ";";
    int playerId, positionX, positionY, rotation;

    char *t = strtok(buffer, del);

    if(t == nullptr)       
        return;        
    playerId = atoi(t);
    t = strtok(nullptr, del);

    if(t == nullptr)       
        return;        
    positionX = atoi(t);
    t = strtok(nullptr, del);

    if(t == nullptr)       
        return;        
    positionY = atoi(t);
    t = strtok(nullptr, del);

    if(t == nullptr)       
        return;        
    rotation = atoi(t);

    //printf("Data: %d, %d, %d, %d\n", playerId, positionX, positionY, rotation);
    players[playerId].positionX = positionX;
    players[playerId].positionY = positionY;
    players[playerId].rotation = rotation;
}

void udpHandleMultipleMessagesFromClient(){
    char buffer[255];
    
    while(true) {
        int count = recvfrom(udpFd, buffer, 255, MSG_DONTWAIT, nullptr, nullptr);
        
        if (count < 0) {
            if (errno == EAGAIN || errno == EWOULDBLOCK) {
                break;
            }
            break;
        }
        
        udpHandleMessageFromClient(buffer);
    }
}

void udpSendMessageToPlayers(){
    std::string buffer = "M;";
    for(const auto& i : players){
        if(!i.second.isAlive)
            continue;
        buffer = buffer + std::to_string(i.second.playerId) + ";" + std::to_string(i.second.positionX) + ";" + std::to_string(i.second.positionY) + ";" + std::to_string(i.second.rotation) + "!";
    }
    buffer += "~";
    int count = buffer.size();
    //std::cout << "UDP Update: " << buffer << std::endl;
    for (const auto& i : players) {
        sendto(udpFd, buffer.c_str(), count, 0, (sockaddr *)&i.second.playerAddr, sizeof(i.second.playerAddr));
        //std::cout << "UDP Update: "<< i.first << ": " << buffer << std::endl;
    }
}

void bulletsUpdate(int ms) {
    for (auto it = bullets.begin(); it != bullets.end(); ) {
        
        // Dla wygody tworzymy referencję do pocisku
        auto& bulletData = it->second; // to samo co bullets[i.first]
        bool bulletRemoved = false;
        //int s = ms/1000;
        // 1. Aktualizacja pozycji
        bulletData.positionX += 100 * bulletData.speed * cos(bulletData.direction * M_PI / 180.0)*ms/1000;
        bulletData.positionY += 100 *bulletData.speed * sin(bulletData.direction * M_PI / 180.0)*ms/1000;

        // 2. Sprawdzanie kolizji z graczami
        for (const auto& playerEntry : players) {
            const auto& player = playerEntry.second;

            if (player.playerId == bulletData.ownerPlayerId || !player.isAlive)
                continue; 

            long dx = bulletData.positionX - player.positionX;
            long dy = bulletData.positionY - player.positionY;
            long distanceSquared = dx * dx + dy * dy;
            long collisionDistance = 65; 

            if (distanceSquared <= collisionDistance * collisionDistance) {
                
                players[player.playerId].health -= bulletData.damage;
                
                printf("Player %d hit by bullet %d. Health: %d\n",
                       player.playerId, bulletData.bulletId, players[player.playerId].health);

                
                if (players[player.playerId].health <= 0) {
                    players[player.playerId].isAlive = false;
                    printf("Player %d has died.\n", player.playerId);
                    players[bulletData.ownerPlayerId].points += 100;
                    players[player.playerId].health = 0;
                }

             
                std::string hitmessage = "H;" + std::to_string(player.playerId) + ";" + 
                                         std::to_string(bulletData.bulletId) + ";" + 
                                         std::to_string(players[player.playerId].health) + ";" + 
                                         std::to_string(players[player.playerId].isAlive) + "~";
                tcpSendMessageToPlayers(hitmessage);

               
                it = bullets.erase(it); 
                bulletRemoved = true;
                
                break; 
            }
        }

        
        if (bulletRemoved) {
            continue;
        }

        bulletData.lifetime -= 1;

        if (bulletData.lifetime <= 0) {
            std::string mess = "E;" + std::to_string(bulletData.bulletId) + "~";
            tcpSendMessageToPlayers(mess);
            it = bullets.erase(it);
        } else {
            ++it;
        }
    }
}

void tcpSendMessageToPlayers(std::string buffer)
{
    std::unordered_set<int> bad;
    int count = buffer.size();

    for (const auto& i : players) {
        if (count != send(i.second.tcpFd, buffer.c_str(), count, MSG_DONTWAIT))
            bad.insert(i.second.tcpFd);    
    }

    for (int clientFd : bad) {
        printf("Write failed on %d\n", clientFd);
        handlePlayerDisconnect(clientFd);
    }
}

int main(int argc, char **argv) {
    if (argc != 3) error(1, 0, "Usage: %s <tcp-port> <udp-port>", argv[0]);

    tcpPort = atoi(argv[1]);
    udpPort = atoi(argv[2]);

    if (tcpPort == udpPort)
        error(1, 0, "TCP and UDP ports must differ");
    if (tcpPort > 65535 || tcpPort < 1024 || udpPort > 65535 || udpPort < 1024)
        error(1, 0, "Ports must be in range 1024-65535");

    srand(time(0));

    tcpFd = getaddrinfo_socket_bind_listen(SOCK_STREAM, argv[1]);
    setNonBlocking(tcpFd);
    int flag = 1;
    setsockopt(tcpFd, IPPROTO_TCP, O_NDELAY, (char *)&flag, sizeof(int));
    udpFd = getaddrinfo_socket_bind_listen(SOCK_DGRAM, argv[2]);
    setNonBlocking(udpFd);
    // set up ctrl+c handler for a graceful exit
    signal(SIGINT, ctrl_c);
    // prevent dead sockets from throwing pipe errors on write
    signal(SIGPIPE, SIG_IGN);

    /****************************/

    epollDesc = epoll_create1(0);

    std::cout << "Epoll created, desc: " << epollDesc << std::endl;

    epoll_event ee;
    ee.events = POLL_IN;
    ee.data.fd = STDOUT_FILENO;
    epoll_ctl(epollDesc, EPOLL_CTL_ADD, STDIN_FILENO, &ee);
    ee.data.u32 = -1;
    epoll_ctl(epollDesc, EPOLL_CTL_ADD, tcpFd, &ee);
    ee.data.u32 = -2;
    epoll_ctl(epollDesc, EPOLL_CTL_ADD, udpFd, &ee);

    int cooldown = 1000 / ticks;
    auto lastTimeSendUdp = std::chrono::high_resolution_clock::now();
    int tickCount = 0;

    while (true) {
        auto dur = std::chrono::high_resolution_clock::now() - lastTimeSendUdp;
        auto ms = std::chrono::duration_cast<std::chrono::milliseconds>(dur).count();

        if(ms >= cooldown){
            //printf("Sent UDP update to players\n");
            udpSendMessageToPlayers();
            tickCount++;
            if(tickCount % ticks == 0){
                tickCount = 0;
                std::string scoreUpdate = "P;";
                for(const auto& i : players){
                    if(i.second.isAlive)
                        players[i.first].points += 1;
                }
                for(const auto& i : players){
                    if(!i.second.isAlive)
                        continue;
                    scoreUpdate = scoreUpdate + std::to_string(i.second.playerId) + ";" + std::to_string(i.second.points) + "!";
                    
                }
                scoreUpdate += "~";
                tcpSendMessageToPlayers(scoreUpdate);
            }
            bulletsUpdate(ms);
            lastTimeSendUdp = std::chrono::high_resolution_clock::now();
            continue;
        }

        int timeout = cooldown - ms;
        if (timeout < 0) timeout = 0;


        if(epoll_wait(epollDesc, &ee, 1, timeout) <= 0){
            continue;
        }

        if(ee.data.u32 == (uint32_t)-1){
            handleNewClient(ee);          
            continue;
        }   
        
        if(ee.data.u32 == (uint32_t)-2){
            udpHandleMultipleMessagesFromClient();          
            continue;
        } 

        tpcHandleMessageFromClient(ee);
    }
}

void ctrl_c(int) {
    const char quitMsg[] = "[Server shutting down. Bye!]\n";
    sendToAllBut(-1, quitMsg, sizeof(quitMsg));

    for (const auto& i : players) {
        shutdown(i.second.tcpFd, SHUT_RDWR);
        // note that if the message cannot be sent out immediately,
        // it will be discarded upon 'close'
        close(i.second.tcpFd); 
    }
    close(tcpFd);
    printf("Closing server\n");
    exit(0);
}

void sendToAllBut(int fd, const char *buffer, int count) {
    std::unordered_set<int> bad;

    for (const auto& i : players) {
        if (i.second.tcpFd == fd)
            continue;
        if (count != send(i.second.tcpFd, buffer, count, MSG_DONTWAIT))
            bad.insert(i.second.tcpFd);    
    }

    // 'read' handles errors on a client socket, but it won't do a thing when
    // the client has gone away without notice. Hwnce, if the system send
    // buffer overflows, then the 'shutdown' below triggers 'read' to fail.
    for (int clientFd : bad) {
        printf("Write failed on %d\n", clientFd);
        handlePlayerDisconnect(clientFd);
        // clientFds.erase(clientFd);
        // shutdown(clientFd, SHUT_RDWR);
    }
}

int getaddrinfo_socket_bind_listen(int sockType, const char *port) {

    // resolve port to a 'sockaddr*' for a TCP server
    addrinfo *res, hints{};
    hints.ai_flags = AI_PASSIVE;
    // hints.ai_socktype = SOCK_STREAM;
    hints.ai_socktype = sockType;
    int rv = getaddrinfo(nullptr, port, &hints, &res);
    if (rv) error(1, 0, "getaddrinfo: %s", gai_strerror(rv));

    // create socket
    int sockFd = socket(res->ai_family, res->ai_socktype, res->ai_protocol);
    if (sockFd == -1) error(1, errno, "socket failed");

    char result_host[NI_MAXHOST], result_port[NI_MAXSERV];
    getnameinfo(res->ai_addr, res->ai_addrlen, result_host, NI_MAXHOST, result_port, NI_MAXSERV, 0);


    // TO DO - add error when result port is different than requested;

    if(atoi(port) != atoi(result_port)){
        printf("Failed creating socket at port: %s\n", port);
        exit(1);
    }

    printf("Created socket: %s:%s (fd: %d)\n", result_host, result_port, sockFd);

    // try to set REUSEADDR
    const int one = 1;
    setsockopt(sockFd, SOL_SOCKET, SO_REUSEADDR, &one, sizeof(one));

    // bind to address as resolved by getaddrinfo
    if (bind(sockFd, res->ai_addr, res->ai_addrlen))
        error(1, errno, "bind failed");

    // enter listening mode
    if(sockType == SOCK_STREAM){
        if (listen(sockFd, 1))
            error(1, errno, "listen failed");
    }
    
    return sockFd;
}
