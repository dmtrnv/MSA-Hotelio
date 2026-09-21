const HOTEL_API_URL = process.env.HOTEL_API_URL;

export async function getHotelById(hotelId) {
  const response = await fetch(
    `${HOTEL_API_URL}/${encodeURIComponent(hotelId)}`
  );

  if (response.status === 404) {
    return null;
  }
  if (!response.ok) {
    throw new Error(
      `Hotel API request failed: ${response.status} ${response.statusText}`
    );
  }

  const hotel = await response.json();
  return {
    id: hotel.id,
    name: hotel.description,
    city: hotel.city,
    stars: Math.floor(hotel.rating),
  };
}