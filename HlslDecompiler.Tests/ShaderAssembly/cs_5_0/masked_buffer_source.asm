cs_5_0
dcl_globalFlags refactoringAllowed | enable11_1ShaderExtensions
dcl_constantbuffer CB0[2], immediateIndexed
dcl_resource_structured t0, 8
dcl_uav_structured u0, 16
dcl_input vThreadID.x
dcl_temps 2
dcl_thread_group 64, 1, 1
ld_structured_indexable(structured_buffer, stride=8)(mixed,mixed,mixed,mixed) r0.xy, vThreadID.xx, l(0), t0.xy
ushr r1.xyz, r0.xxx, l(8, 16, 24, 0)
bfi r0.yzw, l(0, 8, 16, 24), l(0, 24, 16, 8), r0.yyy, r1.xyz
msad r0, cb0[0].x, r0, cb0[1]
store_structured u0, vThreadID.x, l(0), r0
ret
