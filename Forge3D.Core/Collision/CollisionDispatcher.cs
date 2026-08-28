using System.Numerics;

namespace Forge3D.Core.Collision;

public sealed class CollisionDispatcher
{
    public bool TryGenerateContact(Collider colliderA, Collider colliderB, out Contact contact)
    {
        if (colliderA is PlaneCollider && colliderB is not PlaneCollider)
        {
            var hit = TryGenerateContact(colliderB, colliderA, out contact);
            if (hit)
            {
                contact = contact with
                {
                    BodyA = colliderA.Body,
                    BodyB = colliderB.Body,
                    Normal = -contact.Normal
                };
            }

            return hit;
        }

        contact = default;

        return (colliderA, colliderB) switch
        {
            (SphereCollider sphereA, SphereCollider sphereB) => TrySphereSphere(sphereA, sphereB, out contact),
            (SphereCollider sphere, PlaneCollider plane) => TrySpherePlane(sphere, plane, out contact),
            (BoxCollider box, PlaneCollider plane) => TryBoxPlane(box, plane, out contact),
            (SphereCollider sphere, BoxCollider box) => TrySphereBox(sphere, box, out contact),
            (BoxCollider box, SphereCollider sphere) => TryBoxSphere(box, sphere, out contact),
            (BoxCollider boxA, BoxCollider boxB) => TryBoxBox(boxA, boxB, out contact),
            _ => false
        };
    }

    private static bool TrySphereSphere(SphereCollider a, SphereCollider b, out Contact contact)
    {
        var delta = b.Body.Position - a.Body.Position;
        var distanceSquared = delta.LengthSquared();
        var radiusSum = a.Radius + b.Radius;

        if (distanceSquared >= radiusSum * radiusSum)
        {
            contact = default;
            return false;
        }

        var distance = MathF.Sqrt(distanceSquared);
        var normal = distance > 0.0001f ? delta / distance : Vector3.UnitY;
        var point = a.Body.Position + normal * (a.Radius - ((radiusSum - distance) * 0.5f));

        contact = CreateContact(a, b, point, normal, radiusSum - distance);
        return true;
    }

    private static bool TrySpherePlane(SphereCollider sphere, PlaneCollider plane, out Contact contact)
    {
        var signedDistance = Vector3.Dot(sphere.Body.Position, plane.Normal) - plane.DistanceFromOrigin;
        var penetration = sphere.Radius - signedDistance;

        if (penetration <= 0.0f)
        {
            contact = default;
            return false;
        }

        var point = sphere.Body.Position - plane.Normal * sphere.Radius;
        contact = CreateContact(sphere, plane, point, -plane.Normal, penetration);
        return true;
    }

    private static bool TryBoxPlane(BoxCollider box, PlaneCollider plane, out Contact contact)
    {
        var support = box.GetCorners()
            .OrderBy(corner => Vector3.Dot(corner, plane.Normal))
            .First();

        var signedDistance = Vector3.Dot(support, plane.Normal) - plane.DistanceFromOrigin;
        if (signedDistance > 0.0f)
        {
            contact = default;
            return false;
        }

        contact = CreateContact(box, plane, support, -plane.Normal, -signedDistance);
        return true;
    }

    private static bool TrySphereBox(SphereCollider sphere, BoxCollider box, out Contact contact)
    {
        var closest = box.ClosestPoint(sphere.Body.Position);
        var delta = sphere.Body.Position - closest;
        var distanceSquared = delta.LengthSquared();

        if (distanceSquared > sphere.Radius * sphere.Radius)
        {
            contact = default;
            return false;
        }

        var distance = MathF.Sqrt(distanceSquared);
        var normal = distance > 0.0001f ? -delta / distance : Vector3.UnitY;
        contact = CreateContact(sphere, box, closest, normal, sphere.Radius - distance);
        return true;
    }

    private static bool TryBoxSphere(BoxCollider box, SphereCollider sphere, out Contact contact)
    {
        var hit = TrySphereBox(sphere, box, out contact);
        if (hit)
        {
            contact = contact with
            {
                BodyA = box.Body,
                BodyB = sphere.Body,
                Normal = -contact.Normal
            };
        }

        return hit;
    }

    private static bool TryBoxBox(BoxCollider a, BoxCollider b, out Contact contact)
    {
        if (!a.ComputeBounds().Intersects(b.ComputeBounds()))
        {
            contact = default;
            return false;
        }

        var axes = GetSatAxes(a, b);
        var bestAxis = Vector3.Zero;
        var bestOverlap = float.PositiveInfinity;

        foreach (var axis in axes)
        {
            if (axis.LengthSquared() <= 0.000001f)
            {
                continue;
            }

            var normalizedAxis = Vector3.Normalize(axis);
            Project(a.GetCorners(), normalizedAxis, out var minA, out var maxA);
            Project(b.GetCorners(), normalizedAxis, out var minB, out var maxB);
            var overlap = MathF.Min(maxA, maxB) - MathF.Max(minA, minB);

            if (overlap <= 0.0f)
            {
                contact = default;
                return false;
            }

            if (overlap < bestOverlap)
            {
                bestOverlap = overlap;
                bestAxis = normalizedAxis;
            }
        }

        if (Vector3.Dot(b.Body.Position - a.Body.Position, bestAxis) < 0.0f)
        {
            bestAxis = -bestAxis;
        }

        var point = (a.ClosestPoint(b.Body.Position) + b.ClosestPoint(a.Body.Position)) * 0.5f;
        contact = CreateContact(a, b, point, bestAxis, bestOverlap);
        return true;
    }

    private static List<Vector3> GetSatAxes(BoxCollider a, BoxCollider b)
    {
        var axesA = a.GetAxes();
        var axesB = b.GetAxes();
        var axes = new List<Vector3>(15);
        axes.AddRange(axesA);
        axes.AddRange(axesB);

        foreach (var axisA in axesA)
        {
            foreach (var axisB in axesB)
            {
                axes.Add(Vector3.Cross(axisA, axisB));
            }
        }

        return axes;
    }

    private static void Project(IReadOnlyList<Vector3> points, Vector3 axis, out float min, out float max)
    {
        min = Vector3.Dot(points[0], axis);
        max = min;

        for (var i = 1; i < points.Count; i++)
        {
            var value = Vector3.Dot(points[i], axis);
            min = MathF.Min(min, value);
            max = MathF.Max(max, value);
        }
    }

    private static Contact CreateContact(Collider a, Collider b, Vector3 point, Vector3 normal, float penetration)
    {
        return new Contact(
            a.Body,
            b.Body,
            point,
            Vector3.Normalize(normal),
            MathF.Max(0.0f, penetration),
            MathF.Sqrt(a.Material.Friction * b.Material.Friction),
            MathF.Max(a.Material.Restitution, b.Material.Restitution));
    }
}
