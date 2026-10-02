import { ApolloServer } from '@apollo/server';
import { startStandaloneServer } from '@apollo/server/standalone';
import { buildSubgraphSchema } from '@apollo/subgraph';
import { GraphQLError } from 'graphql';
import { getHotelById } from './hotelClient.js';
import gql from 'graphql-tag';


const typeDefs = gql`
  type Hotel @key(fields: "id") {
    id: ID!
    name: String
    city: String
    stars: Int
  }

  type Query {
    hotelsByIds(ids: [ID!]!): [Hotel]
  }
`;

const resolvers = {
  Hotel: {
    __resolveReference: async ({ id }) => {
      try {
        return await getHotelById(id);
      } catch (error) {
        console.error('Get hotels HTTP error:', error);

        throw new GraphQLError('Failed to fetch hotels', {
          extensions: {
            code: 'INTERNAL_SERVER_ERROR',
          },
        });
      }
    },
  },
  Query: {
    hotelsByIds: async (_, { ids }) => {
      try {
        const hotels = await Promise.all(
            ids.map((id) => getHotelById(id))
        );
        return hotels.filter((hotel) => hotel !== null);
      } catch (error) {
        console.error('Get hotels HTTP error:', error);

        throw new GraphQLError('Failed to fetch hotels', {
          extensions: {
            code: 'INTERNAL_SERVER_ERROR',
          },
        });
      }
    },
  },
};

const server = new ApolloServer({
  schema: buildSubgraphSchema([{ typeDefs, resolvers }]),
});

startStandaloneServer(server, {
  listen: { port: 4002 },
}).then(() => {
  console.log('✅ Hotel subgraph ready at http://localhost:4002/');
});
