ps_4_0
dcl_constantbuffer cb0[5], immediateIndexed
dcl_sampler s0, mode_comparison
dcl_sampler s1, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_resource_texture2d (float,float,float,float) t1
dcl_input_ps linear v1
dcl_input_ps linear v2.xy
dcl_output o0
dcl_temps 3
dp4 r0.x, v1, cb0[0]
dp4 r0.y, v1, cb0[1]
dp4 r0.z, v1, cb0[2]
dp4 r0.w, v1, cb0[3]
div r0.xyz, r0.xyz, r0.www
mad r0.xy, r0.xy, l(0.5, -0.5, 0, 0), l(0.5, 0.5, 0, 0)
add r0.z, r0.z, -cb0[4].x
mov r1.yw, l(0, -1082130432, 0, 0)
mov r0.w, l(0)
mov r2.x, l(-1)
loop
ilt r2.y, l(1), r2.x
breakc_nz r2.y
itof r1.x, r2.x
mad r2.yz, r1.xy, cb0[4].zw, r0.xy
sample_c_lz r1.x, r2.y, t0.x, s0, r0.z
add r0.w, r0.w, r1.x
iadd r2.x, r2.x, l(1)
endloop
mov r1.x, r0.w
mov r1.y, l(-1)
loop
ilt r2.x, l(1), r1.y
breakc_nz r2.x
itof r1.z, r1.y
mad r2.xy, r1.zw, cb0[4].zw, r0.xy
sample_c_lz r1.z, r2.x, t0.x, s0, r0.z
add r1.x, r1.z, r1.x
iadd r1.y, r1.y, l(1)
endloop
mov r2.y, l(1065353216)
mov r0.w, r1.x
mov r1.y, l(-1)
loop
ilt r1.z, l(1), r1.y
breakc_nz r1.z
itof r2.x, r1.y
mad r1.zw, r2.xy, cb0[4].zw, r0.xy
sample_c_lz r1.z, r1.z, t0.x, s0, r0.z
add r0.w, r0.w, r1.z
iadd r1.y, r1.y, l(1)
endloop
sample r1, v2.xyxx, t1, s1
mul r0.x, r0.w, l(0.111111112)
mul o0, r0.x, r1
ret
