import grpc from '@grpc/grpc-js';
import protoLoader from '@grpc/proto-loader';


const BOOKING_GRPC_URL = process.env.BOOKING_GRPC_URL;

const packageDefinition = protoLoader.loadSync(
    './protos/booking.proto',
    {
        keepCase: false,
        longs: String,
        enums: String,
        defaults: true,
        oneofs: true,
    }
);

const proto = grpc.loadPackageDefinition(packageDefinition);

const bookingService = proto.booking.BookingService;

const client = new bookingService(
    BOOKING_GRPC_URL,
    grpc.credentials.createInsecure()
);

export function getBookingsByUser(userId) {
    return new Promise((resolve, reject) => {
        client.listBookings(
            { userId },
            (error, response) => {
                if (error) {
                    reject(error);
                    return;
                }

                resolve(response?.bookings ?? []);
            }
        );
    });
}