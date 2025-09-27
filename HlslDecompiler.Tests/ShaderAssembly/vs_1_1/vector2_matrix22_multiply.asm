vs_1_1
dcl_position v0
mul r0, v0.xyyx, c0.xyxy
add oPos.xz, r0.yw, r0.xz
mul r0, v0.xyyx, c1.xyxy
add oPos.yw, r0.yw, r0.xz
max r0.xy, -v0.yx, v0.yx
mul r0.zw, r0.xy, c0.xy
mul r0.xy, r0.xy, c1.xy
add oT1.xy, r0.wy, r0.zx
add r0.xy, v0.xy, v0.xy
mul r0.zw, r0.xy, c0.xy
mul r0.xy, r0.xy, c1.xy
add oT2.y, r0.y, r0.x
add oT2.x, r0.w, r0.z
