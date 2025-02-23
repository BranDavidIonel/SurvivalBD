using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Blueprint : MonoBehaviour
{
    public string itemName;
    public int numberOfItemsToProduce;
    public string Req1;
    public string Req2;
    public int Req1amount;
    public int Req2amount;
    public int numeOfRequirements;

    public Blueprint(string name, int producedItems ,int reqNum,string R1, int R1num, string R2, int R2num)
    {
        itemName = name;

        numberOfItemsToProduce = producedItems;

        numeOfRequirements = reqNum;

        Req1 = R1;
        Req2 = R2;

        Req1amount = R1num;
        Req2amount = R2num;
    }
}
