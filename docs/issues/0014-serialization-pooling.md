# Improvement: Message serialization and transport copies are unpooled and duplicated

- Type: improvement
- Status: done
- Workflow: ../specs/issues.md

## Problem

Every message pays multiple heap allocations and byte copies with no
pooling anywhere:

- Send path (`TcpTransport.Send`, `TcpTransport.cs:146-190`): serializer
  output `byte[]` -> per-send UTF-8 `typeTag` re-encoded from the type
  name (cacheable at registration) -> `frame` array -> prefixed `bytes`
  array - 4 allocations and 3 copies per message.
- Receive path (`ReceiveLoop`, `TcpTransport.cs:311-372`): `frame` array
  per frame, then a second `payload` array copy, then generated `Unpack`
  allocates its `byte[] Payload` fields again.
- Generated serializers (`Serialize()` -> `GetSize()` walk +
  `new byte[GetSize()]` + `SerializeInto` walk) allocate per call; codecs
  (`NsdComponentCodec<T>`, `TransformCodec`, `PhysicsCodec`,
  `IdentifierCodec`) return new arrays per component.
- `NsdComponentCodec.Serialize` returns `[]` (a fresh empty array) when
  the component is absent (`NsdComponentCodec.cs:39`).

At 60 Hz x entities x clients this is hundreds of thousands of
allocations per second on the host.

## Acceptance criteria

- [x] `ArrayPool<byte>` (or equivalent) used for frame buffers and
      deserialization scratch; buffers are returned to the pool after
      send/deserialize.
- [x] Type names resolved to bytes once at registration and reused per
      send (`SerializerCache`).
- [x] Receive path avoids the frame -> payload double copy where a single
      slice suffices.
- [x] Allocation profile measured before/after (e.g. alloc counter in the
      replication benchmark) demonstrating a majority reduction on the
      send/receive hot path.
- [x] Correctness preserved: pooled buffers are never retained past the
      send/deserialize call (transport queues own their copies).