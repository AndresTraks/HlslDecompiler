ds_5_0
dcl_input_control_point_count 3
dcl_tessellator_domain domain_tri
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_input vDomain.x
dcl_input vicp[3][0]
dcl_input vicp[3][1]
dcl_input vicp[3][2]
dcl_input vicp[3][3]
dcl_input vicp[3][4]
dcl_output_siv o0, position
dcl_temps 1
dcl_indexrange vicp[3][1] 4
ishr r0.xy, cb0[0].xx, l(2, 1, 0, 0)
and r0.xy, r0.xy, l(1, 1, 0, 0)
and r0.zw, cb0[0].xx, l(0, 0, 3, 1)
mad r0, vicp[r0.w][r0.z + 1], vDomain.x, vicp[r0.y][r0.x + 2]
add o0, r0, vicp[2][0]
ret
