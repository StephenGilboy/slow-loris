const http = require('http');

const server = http.createServer((req, res) => {
    res.write('Hello');
    res.end();
    server.getConnections((err, count) => {
        console.log(`There are ${count} live connections`);
    });
});

server.on('connection', (socket) => {
    console.log(`Connection from ${socket.remoteAddress}`);
    server.getConnections((err, count) => {
        console.log(`There are ${count} live connections`);
    });
});

server.on('error', (err) => {
    console.error(err);
    server
        .close()
        .once('close', () => {
            process.exit(1);1
        });
});

server.listen(8000).once('listening', () => console.log(`Listeing on port 8000`));
