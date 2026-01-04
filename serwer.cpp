#include <cstdio>
#include <netdb.h>
#include <signal.h>
#include <sys/socket.h>
#include <unistd.h>
#include <unordered_set>

#include <sys/epoll.h>

#include <cstdlib>
#include <cstring>
#include <errno.h>
#include <string>
#include <map>

// server socket
int tcpFd;
int updFd;
int epollDesc;

class PlayerData {      
  public:           
    int tcpFd;
    int udpFd;
    std::string name;

    bool isAlive;
    int health;

    int positionX;
    int positionY;
    int rotation;
};

// client sockets
std::unordered_set<int> clientFds;  
std::map<int, PlayerData> players;


template <typename... Args> /* This mimics GNU 'error' function */
void error(int status, int errnum, const char *format, Args... args) {
    fprintf(stderr, (format + std::string(errnum ? ": %s\n" : "\n")).c_str(), args..., strerror(errnum));
    if (status) exit(status);
}

// creates a socket listening on port specified in argv[1]
// int getaddrinfo_socket_bind_listen(int argc, const char *const *argv);
int getaddrinfo_socket_bind_listen(int sockType, const char *argv);

// handles SIGINT
void ctrl_c(int);

// sends data to clientFds excluding fd
void sendToAllBut(int fd, const char *buffer, int count);

int getRandomNumberInRange(int min, int max){
    int randomNum = min + rand() % (max - min);
    return randomNum; 
}

void handleNewClient(epoll_event ee){
    // prepare placeholders for client address
    sockaddr_storage clientAddr{0};
    socklen_t clientAddrSize = sizeof(clientAddr);

    // accept new connection
    auto clientFd = accept(tcpFd, (sockaddr *)&clientAddr, &clientAddrSize);
    if (clientFd == -1)
        perror("accept failed");

    // add client to all clients set
    clientFds.insert(clientFd);

    // tell who has connected
    char host[NI_MAXHOST], port[NI_MAXSERV];
    getnameinfo((sockaddr *)&clientAddr, clientAddrSize, host, NI_MAXHOST, port, NI_MAXSERV, 0);
    printf("New connection from: %s:%s (fd: %d)\n", host, port, clientFd);


    ee.data.fd = clientFd;
    epoll_ctl(epollDesc, EPOLL_CTL_ADD, clientFd, &ee);

    send(clientFd, "1235:100", 9, 0);
}

void tpcHandleMessageFromClient(epoll_event ee){
    int clientFd = ee.data.fd;

    char buffer[255];
    int count = read(clientFd, buffer, 255);


    if (count < 1) {
        printf("Client disconnected: %d\n", clientFd);
        clientFds.erase(clientFd);
        close(clientFd);
        return;
    }
    
    if(clientFd != STDOUT_FILENO)
        write(STDOUT_FILENO, buffer, count);
    sendToAllBut(clientFd, buffer, count);
}


void udpHandleMessageFromClient(epoll_event ee){
    int clientFd = ee.data.fd;

    char buffer[255];
    int count = read(updFd, buffer, 255);

    printf("Received UDP message: %s\n", buffer);

    const char *del = ":";
    int positionX, positionY, rotation;

    char *t = strtok(buffer, del);

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
    t = strtok(nullptr, del);

    
    printf("Data: %d, %d, %d\n", positionX, positionY, rotation);
}

int main(int argc, char **argv) {
    if (argc != 3) error(1, 0, "Usage: %s <tcp-port> <udp-port>", argv[0]);
    if (atoi(argv[1]) == atoi(argv[2]))
        error(1, 0, "TCP and UDP ports must differ");
    if (atoi(argv[1]) > 65535 || atoi(argv[1]) < 1024 || atoi(argv[2]) > 65535 || atoi(argv[2]) < 1024)
        error(1, 0, "Ports must be in range 1024-65535");

    srand(time(0));

    tcpFd = getaddrinfo_socket_bind_listen(SOCK_STREAM, argv[1]);
    updFd = getaddrinfo_socket_bind_listen(SOCK_DGRAM, argv[2]);
    // set up ctrl+c handler for a graceful exit
    signal(SIGINT, ctrl_c);
    // prevent dead sockets from throwing pipe errors on write
    signal(SIGPIPE, SIG_IGN);

    /****************************/

    epollDesc = epoll_create1(0);

    epoll_event ee;
    ee.events = POLL_IN;
    ee.data.fd = STDOUT_FILENO;
    epoll_ctl(epollDesc, EPOLL_CTL_ADD, STDIN_FILENO, &ee);
    ee.data.u32 = -1;
    epoll_ctl(epollDesc, EPOLL_CTL_ADD, tcpFd, &ee);
    ee.data.u32 = -2;
    epoll_ctl(epollDesc, EPOLL_CTL_ADD, updFd, &ee);


    while (true) {
        if(epoll_wait(epollDesc, &ee, 1, -1) < 0)
            continue;

        if(ee.data.u32 == -1){
            handleNewClient(ee);          
            continue;
        }   
        
        if(ee.data.u32 == -2){
            udpHandleMessageFromClient(ee);          
            continue;
        } 

        tpcHandleMessageFromClient(ee);
    }
}

void ctrl_c(int) {
    const char quitMsg[] = "[Server shutting down. Bye!]\n";
    sendToAllBut(-1, quitMsg, sizeof(quitMsg));
    for (int clientFd : clientFds) {
        shutdown(clientFd, SHUT_RDWR);
        // note that if the message cannot be sent out immediately,
        // it will be discarded upon 'close'
        close(clientFd); 
    }
    close(tcpFd);
    printf("Closing server\n");
    exit(0);
}

void sendToAllBut(int fd, const char *buffer, int count) {
    decltype(clientFds) bad;
    for (int clientFd : clientFds) {
        if (clientFd == fd)
            continue;
        if (count != send(clientFd, buffer, count, MSG_DONTWAIT))
            bad.insert(clientFd);
    }
    // 'read' handles errors on a client socket, but it won't do a thing when
    // the client has gone away without notice. Hwnce, if the system send
    // buffer overflows, then the 'shutdown' below triggers 'read' to fail.
    for (int clientFd : bad) {
        printf("write failed on %d\n", clientFd);
        clientFds.erase(clientFd);
        shutdown(clientFd, SHUT_RDWR);
    }
}


// int getaddrinfo_socket_bind_listen(int argc, const char *const *argv) {
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
