import { ApolloServer } from '@apollo/server';
import { startStandaloneServer } from '@apollo/server/standalone';
import { buildSubgraphSchema } from '@apollo/subgraph';
import { GraphQLError } from 'graphql';
import { getBookingsByUser } from './bookingClient.js';
import gql from 'graphql-tag';


const typeDefs = gql`
  type Booking @key(fields: "id") {
    id: ID!
    userId: String!
    hotelId: String!
    promoCode: String
    discountPercent: Int
    hotel: Hotel
  }

  extend type Hotel @key(fields: "id") {
    id: ID! @external
  }

  type Query {
    bookingsByUser(userId: String!): [Booking]
  }

`;

const resolvers = {
  Query: {
    bookingsByUser: async (_, { userId }, { req }) => {
      console.log('Got request for bookings of user ' + userId);
      const userIdFromReq = req.headers['userid'];
      if (userIdFromReq !== userId) {
        console.error('User ' + userIdFromReq + ' is not authorized to get bookings of user ' + userId);
        throw new GraphQLError('Forbidden', {
          extensions: {
            code: 'FORBIDDEN',
          },
        });
      }

      try {
        let bookings = await getBookingsByUser(userId);
        console.log('Successfully got bookings of user ' + userId);
        return bookings;
      } catch (error) {
        console.error('Booking gRPC error:', error);

        throw new GraphQLError('Failed to fetch bookings', {
          extensions: {
            code: 'INTERNAL_SERVER_ERROR',
          },
        });
      }
    },
  },
  Booking: {
    hotel: (booking) => ({
      __typename: 'Hotel',
      id: booking.hotelId,
    }),
  },
};

const server = new ApolloServer({
  schema: buildSubgraphSchema([{ typeDefs, resolvers }]),
});

startStandaloneServer(server, {
  listen: { port: 4001 },
  context: async ({ req }) => ({ req }),
}).then(() => {
  console.log('✅ Booking subgraph ready at http://localhost:4001/');
});
