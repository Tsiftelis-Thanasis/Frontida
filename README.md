# Frontida (Φροντίδα)

A simplified caregiving platform connecting families with trusted caregivers in Greece.

## Overview

Frontida is a streamlined web application that helps families find and connect with verified babysitters, tutors, elderly caregivers, and other household help. Built with modern .NET technologies, it provides a simple and efficient way to manage caregiving services.

## Features

- **Family Profiles**: Easy registration and profile management for families seeking care
- **Caregiver Listings**: Browse and search verified caregivers by service type and location
- **Service Categories**: Childcare, elderly care, tutoring, housekeeping, and pet care
- **Booking System**: Simple scheduling and appointment management
- **Reviews & Ratings**: Build trust through community feedback
- **Secure Messaging**: Direct communication between families and caregivers

## Tech Stack

- **Backend**: .NET 10
- **Database**: SQL Server
- **Frontend**: ASP.NET Core MVC / Razor Pages
- **Authentication**: ASP.NET Core Identity
- **ORM**: Entity Framework Core

## Prerequisites

- .NET 10 SDK
- SQL Server 2019 or later (or SQL Server Express)
- Visual Studio 2022 or VS Code with C# extension

## Getting Started

### Installation

1. Clone the repository:
```bash
git clone https://github.com/yourusername/frontida.git
cd frontida
```

2. Restore dependencies:
```bash
dotnet restore
```

3. Update the connection string in `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=FrontidaDB;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

4. Apply database migrations:
```bash
dotnet ef database update
```

5. Run the application:
```bash
dotnet run
```

6. Navigate to `https://localhost:5001` in your browser

## Project Structure

```
Frontida/
├── Controllers/          # MVC controllers
├── Models/              # Data models and view models
├── Views/               # Razor views
├── Data/                # DbContext and migrations
├── Services/            # Business logic services
├── wwwroot/             # Static files (CSS, JS, images)
└── appsettings.json     # Configuration
```

## Database Schema

Core entities:
- **Users**: Family and caregiver accounts
- **Profiles**: Detailed user profiles with verification status
- **Services**: Available caregiving services
- **Bookings**: Appointment and scheduling information
- **Reviews**: Ratings and feedback

## Development Roadmap

- [ ] Basic user authentication and authorization
- [ ] Caregiver profile creation and verification
- [ ] Family profile and search functionality
- [ ] Booking and scheduling system
- [ ] Messaging between users
- [ ] Review and rating system
- [ ] Payment integration
- [ ] Mobile responsiveness
- [ ] Greek and English localization

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the project
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Contact

Project Link: [https://github.com/yourusername/frontida](https://github.com/yourusername/frontida)

## Acknowledgments

- Inspired by the need for simple, trustworthy caregiving connections in Greece
- Built with modern .NET technologies for reliability and performance
