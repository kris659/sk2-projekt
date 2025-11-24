#include <cstdio>
#include <netdb.h>
#include <signal.h>
#include <sys/socket.h>
#include <unistd.h>
#include <unordered_set>

#include <poll.h>
#include <sys/epoll.h>
#include <thread>
#include <mutex>

// creates a socket listening on port specified in argv[1]
int getaddrinfo_socket_bind_listen(int argc, const char *const *argv);

// handles SIGINT
void ctrl_c(int);

// sends data to clientFds excluding fd
void sendToAllBut(int fd, const char *buffer, int count);

// server socket
int tcpFd;

// client sockets
std::unordered_set<int> clientFds;

int main(int argc, char **argv) {
    tcpFd = getaddrinfo_socket_bind_listen(argc, argv);
    // set up ctrl+c handler for a graceful exit
    signal(SIGINT, ctrl_c);
    // prevent dead sockets from throwing pipe errors on write
    signal(SIGPIPE, SIG_IGN);

    /****************************/

    while (true) {
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
        printf("new connection from: %s:%s (fd: %d)\n", host, port, clientFd);

        /****************************/

        // read a message
        char buffer[255];
        int count = read(clientFd, buffer, 255);

        if (count < 1) {
            printf("removing %d\n", clientFd);
            clientFds.erase(clientFd);
            close(clientFd);
            continue;
        } else {
            // broadcast the message
            sendToAllBut(clientFd, buffer, count);
        }

        /****************************/
    }

    /****************************/
}



#include <cstdlib>
#include <cstring>
#include <errno.h>
#include <string>

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

template <typename... Args> /* This mimics GNU 'error' function */
void error(int status, int errnum, const char *format, Args... args) {
    fprintf(stderr, (format + std::string(errnum ? ": %s\n" : "\n")).c_str(), args..., strerror(errnum));
    if (status) exit(status);
}

int getaddrinfo_socket_bind_listen(int argc, const char *const *argv) {
    if (argc != 2) error(1, 0, "Usage: %s <port>", argv[0]);

    // resolve port to a 'sockaddr*' for a TCP server
    addrinfo *res, hints{};
    hints.ai_flags = AI_PASSIVE;
    hints.ai_socktype = SOCK_STREAM;
    int rv = getaddrinfo(nullptr, argv[1], &hints, &res);
    if (rv) error(1, 0, "getaddrinfo: %s", gai_strerror(rv));

    // create socket
    int tcpFd = socket(res->ai_family, res->ai_socktype, res->ai_protocol);
    if (tcpFd == -1) error(1, errno, "socket failed");

    // try to set REUSEADDR
    const int one = 1;
    setsockopt(tcpFd, SOL_SOCKET, SO_REUSEADDR, &one, sizeof(one));

    // bind to address as resolved by getaddrinfo
    if (bind(tcpFd, res->ai_addr, res->ai_addrlen))
        error(1, errno, "bind failed");

    // enter listening mode
    if (listen(tcpFd, 1))
        error(1, errno, "listen failed");
    return tcpFd;
}
