cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[5], immediateIndexed
dcl_resource_structured t0, 32
dcl_resource_texture2d (float,float,float,float) t1
dcl_resource_texture2d (float,float,float,float) t2
dcl_uav_typed_texture2d (float,float,float,float) u0
dcl_input vThreadIDInGroupFlattened
dcl_input vThreadID.xy
dcl_temps 5
dcl_tgsm_structured g0, 4, 32
dcl_tgsm_raw g1, 4
dcl_thread_group 8, 8, 1
if_z vThreadIDInGroupFlattened.x
store_raw g1.x, l(0), l(0)
endif
sync_g_t
utof r0.xy, vThreadID.xy
add r0.xy, r0.xy, l(0.5, 0.5, 0, 0)
mul r0.xy, r0.xy, cb0[4].yz
mad r0.xy, r0.xy, l(2, 2, 0, 0), l(-1, -1, 0, 0)
mov r1.xy, vThreadID.xy
mov r1.zw, l(0, 0, 0, 0)
ld_indexable(texture2d)(float,float,float,float) r0.z, r1.w, t2.x
mov r0.w, vThreadIDInGroupFlattened.x
loop
uge r2.x, r0.w, cb0[4].x
breakc_nz r2.x
ld_structured_indexable(structured_buffer, stride=32)(mixed,mixed,mixed,mixed) r2, r0.w, l(0), t0
add r2.xyz, -r0.xyz, r2.xyz
dp3 r2.x, r2.xyz, r2.xyz
sqrt r2.x, r2.x
lt r2.x, r2.x, r2.w
if_nz r2.x
imm_atomic_iadd r2.x, g1, l(0), l(1)
ult r2.y, r2.x, l(32)
if_nz r2.y
store_structured g0.x, r2.x, l(0), r0.w
endif
endif
iadd r0.w, r0.w, l(64)
endloop
sync_g_t
ld_indexable(texture2d)(float,float,float,float) r1, r1, t1
ld_raw r0.w, l(0), g1.x
umin r0.w, r0.w, l(32)
mov r2, l(0, 0, 0, 0)
loop
uge r3.x, r2.w, r0.w
breakc_nz r3.x
ld_structured r3.x, r2.w, l(0), g0.x
ld_structured_indexable(structured_buffer, stride=32)(mixed,mixed,mixed,mixed) r4, r3.x, l(0), t0
ld_structured_indexable(structured_buffer, stride=32)(mixed,mixed,mixed,mixed) r3, r3.x, l(16), t0
add r4.xyz, -r0.xyz, r4.xyz
dp3 r4.x, r4.xyz, r4.xyz
sqrt r4.x, r4.x
div r4.x, r4.x, r4.w
add_sat r4.x, -r4.x, l(1)
mul r3.xyz, r3.www, r3.xyz
mul r3.w, r4.x, r4.x
mad r2.xyz, r3.www, r3.xyz, r2.xyz
iadd r2.w, r2.w, l(1)
endloop
mul r1.xyz, r1.xyz, r2.xyz
store_uav_typed u0, vThreadID.xyyy, r1
ret
