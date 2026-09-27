cs_5_0
dcl_globalFlags refactoringAllowed | enable11_1ShaderExtensions
dcl_constantbuffer CB0[2], immediateIndexed
dcl_uav_structured u0, 16
dcl_input vThreadID.x
dcl_temps 1
dcl_thread_group 64, 1, 1
msad r0, cb0[0].x, l(-235736076, -1460538637, -1482100238, -1498961679), cb0[1]
store_structured u0, vThreadID.x, l(0), r0
ret
