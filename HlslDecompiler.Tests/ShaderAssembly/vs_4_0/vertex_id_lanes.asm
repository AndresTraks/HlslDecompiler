vs_4_0
dcl_input_sgv v0.x, vertex_id
dcl_input_sgv v1.x, instance_id
dcl_output_siv o0, position
dcl_temps 1
and r0.x, v0.x, l(1)
ushr r0.z, v0.x, l(1)
utof r0.xy, r0.xz
mad o0.xy, r0.xy, l(2, 2, 0, 0), l(-1, -1, 0, 0)
or o0.z, v1.x, l(1056964608)
mov o0.w, l(1)
ret
