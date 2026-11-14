cs_5_0
dcl_globalFlags refactoringAllowed
dcl_uav_raw u0
dcl_input vThreadID.x
dcl_temps 2
dcl_thread_group 64, 1, 1
imul null, r0.x, vThreadID.x, l(12)
ld_raw_indexable(raw_buffer)(mixed,mixed,mixed,mixed) r0.yzw, r0.xxx, u0.xyz
mul r1.xyz, r0.yzw, l(0.5, 0.5, 0.5, 0)
store_raw u0.xyz, r0.xxx, r1.xyz
dp3 r0.x, r0.yzw, r0.yzw
rsq r0.x, r0.x
mul r0.xyz, r0.xxx, r0.yzw
imad r0.w, vThreadID.x, l(12), l(4096)
store_raw u0.xyz, r0.www, r0.xyz
ret
